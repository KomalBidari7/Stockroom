using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Stockroom.Domain.Common;
using Stockroom.Infrastructure.Data;

namespace Stockroom.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestSecretKey = "StockroomDevSigningKey_ChangeInProd_AtLeast32BytesLong!";
    public const string TestIssuer = "StockroomAPI";
    public const string TestAudience = "StockroomClients";

    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = TestSecretKey,
                ["Jwt:Issuer"] = TestIssuer,
                ["Jwt:Audience"] = TestAudience,
                ["InitialAdmin:Email"] = "admin@stockroom.local",
                ["InitialAdmin:Password"] = "AdminPassword123!"
            });
        });

        // Keep an open in-memory SQLite connection alive for the lifetime of this test host
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        builder.ConfigureServices(services =>
        {
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<StockroomDbContext>));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<StockroomDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });
        });
    }

    public string GenerateJwtToken(
        string userId,
        string email,
        IEnumerable<string> roles,
        DateTime? expiresAt = null,
        string? issuer = null,
        string? audience = null,
        string? secretKey = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey ?? TestSecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expires = expiresAt ?? DateTime.UtcNow.AddHours(1);
        var notBefore = expires < DateTime.UtcNow ? expires.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(-1);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            NotBefore = notBefore,
            Expires = expires,
            Issuer = issuer ?? TestIssuer,
            Audience = audience ?? TestAudience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public HttpClient CreateAuthenticatedClient(params string[] roles)
    {
        var client = CreateClient();
        var token = GenerateJwtToken("test-user-id", "test@stockroom.local", roles);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public HttpClient CreateAdminClient() => CreateAuthenticatedClient(Roles.Admin);
    public HttpClient CreateInventoryManagerClient() => CreateAuthenticatedClient(Roles.InventoryManager);
    public HttpClient CreateViewerClient() => CreateAuthenticatedClient(Roles.Viewer);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection?.Dispose();
    }
}
