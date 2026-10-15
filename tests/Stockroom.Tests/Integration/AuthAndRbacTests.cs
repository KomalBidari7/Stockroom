using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockroom.Application.DTOs.Auth;
using Stockroom.Application.DTOs.Common;
using Stockroom.Application.DTOs.Products;
using Stockroom.Application.DTOs.StockMovements;
using Stockroom.Domain.Common;
using Stockroom.Infrastructure.Data;
using Stockroom.Infrastructure.Identity;
using Xunit;

namespace Stockroom.Tests.Integration;

public class AuthAndRbacTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthAndRbacTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidRequest_AssignsViewerRoleByDefault()
    {
        var email = $"viewer_{Guid.NewGuid():N}@test.local";
        var request = new RegisterRequest
        {
            Email = email,
            Password = "Password123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.Equal(email, authResponse.Email);
        Assert.False(string.IsNullOrWhiteSpace(authResponse.Token));
        Assert.Single(authResponse.Roles);
        Assert.Equal(Roles.Viewer, authResponse.Roles[0]);

        // Verify in database: user has Viewer role and password is not plaintext
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.NotEqual("Password123!", user.PasswordHash);
        Assert.StartsWith("AQAAAA", user.PasswordHash); // Standard ASP.NET Core Identity PBKDF2 hash prefix

        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains(Roles.Viewer, roles);
        Assert.DoesNotContain(Roles.Admin, roles);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409Conflict()
    {
        var email = $"dup_{Guid.NewGuid():N}@test.local";
        var request = new RegisterRequest { Email = email, Password = "Password123!" };

        var first = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "Password123!")]
    [InlineData("valid@test.com", "short")] // Less than 8 characters
    public async Task Register_InvalidData_Returns400BadRequest(string email, string password)
    {
        var request = new RegisterRequest { Email = email, Password = password };
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsSignedJwt()
    {
        var email = $"login_{Guid.NewGuid():N}@test.local";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = "Password123!" });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authResult = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResult);
        Assert.False(string.IsNullOrWhiteSpace(authResult.Token));
        Assert.Equal("Bearer", authResult.TokenType);
        Assert.True(authResult.ExpiresAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task Login_InvalidCredentials_Returns401WithoutUserEnumeration()
    {
        // Non-existent email
        var res1 = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "nonexistent@test.local",
            Password = "Password123!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, res1.StatusCode);
        var prob1 = await res1.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(prob1);
        Assert.Equal("Invalid email or password.", prob1.Detail);

        // Existing email with wrong password
        var email = $"wrongpwd_{Guid.NewGuid():N}@test.local";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = "Password123!" });

        var res2 = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = "IncorrectPassword999!"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, res2.StatusCode);
        var prob2 = await res2.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(prob2);
        // Detail must be identical so attackers cannot enumerate registered emails
        Assert.Equal("Invalid email or password.", prob2.Detail);
    }

    [Fact]
    public async Task TokenValidation_ExpiredOrTamperedTokens_RejectedWith401()
    {
        // Expired token
        var expiredToken = _factory.GenerateJwtToken("user1", "user@test.local", [Roles.Viewer],
            expiresAt: DateTime.UtcNow.AddMinutes(-5));

        var expiredClient = _factory.CreateClient();
        expiredClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);
        var expiredResponse = await expiredClient.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, expiredResponse.StatusCode);

        // Tampered token (modified signature)
        var validToken = _factory.GenerateJwtToken("user1", "user@test.local", [Roles.Viewer]);
        var tamperedToken = validToken[..^4] + "ABCD";

        var tamperedClient = _factory.CreateClient();
        tamperedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);
        var tamperedResponse = await tamperedClient.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, tamperedResponse.StatusCode);

        // Wrong Issuer token
        var wrongIssuerToken = _factory.GenerateJwtToken("user1", "user@test.local", [Roles.Viewer],
            issuer: "UntrustedIssuer");
        var wrongIssuerClient = _factory.CreateClient();
        wrongIssuerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", wrongIssuerToken);
        var wrongIssuerResponse = await wrongIssuerClient.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongIssuerResponse.StatusCode);
    }

    [Fact]
    public async Task CurrentUser_MeEndpoint_ReturnsAuthenticatedProfile()
    {
        var email = $"me_{Guid.NewGuid():N}@test.local";
        var regRes = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = "Password123!" });
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        var meClient = _factory.CreateClient();
        meClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);

        var meRes = await meClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);
        var profile = await meRes.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.NotNull(profile);
        Assert.Equal(email, profile.Email);
        Assert.Contains(Roles.Viewer, profile.Roles);
    }

    [Fact]
    public async Task Viewer_CanRead_CannotModifyOrAdjustStock()
    {
        var viewerClient = _factory.CreateViewerClient();

        // 1. Viewer CAN read products
        var readRes = await viewerClient.GetAsync("/api/products");
        Assert.Equal(HttpStatusCode.OK, readRes.StatusCode);

        // 2. Viewer CAN read reports
        var reportRes = await viewerClient.GetAsync("/api/inventory/summary");
        Assert.Equal(HttpStatusCode.OK, reportRes.StatusCode);

        // 3. Viewer CANNOT create product (403 Forbidden)
        var createRes = await viewerClient.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Illegal Create",
            SKU = $"ILL-{Guid.NewGuid():N}"[..8],
            UnitPrice = 10m
        });
        Assert.Equal(HttpStatusCode.Forbidden, createRes.StatusCode);

        // 4. Viewer CANNOT adjust stock (403 Forbidden)
        var stockRes = await viewerClient.PostAsJsonAsync("/api/products/1/stock-in", new StockAdjustmentRequest
        {
            Quantity = 10
        });
        Assert.Equal(HttpStatusCode.Forbidden, stockRes.StatusCode);

        // 5. Viewer CANNOT delete product (403 Forbidden)
        var deleteRes = await viewerClient.DeleteAsync("/api/products/1");
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);
    }

    [Fact]
    public async Task InventoryManager_CanCreateAndAdjustStock_CannotDelete()
    {
        var mgrClient = _factory.CreateInventoryManagerClient();

        // 1. InventoryManager CAN create product
        var sku = $"MGR-{Guid.NewGuid():N}"[..8];
        var createRes = await mgrClient.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Manager Product",
            SKU = sku,
            UnitPrice = 45m,
            InitialQuantity = 10
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var product = await createRes.Content.ReadFromJsonAsync<ProductResponse>();
        Assert.NotNull(product);

        // 2. InventoryManager CAN adjust stock
        var stockRes = await mgrClient.PostAsJsonAsync($"/api/products/{product.Id}/stock-in", new StockAdjustmentRequest
        {
            Quantity = 5
        });
        Assert.Equal(HttpStatusCode.OK, stockRes.StatusCode);

        // 3. InventoryManager CANNOT delete product (403 Forbidden)
        var deleteRes = await mgrClient.DeleteAsync($"/api/products/{product.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, deleteRes.StatusCode);

        // 4. InventoryManager CANNOT access admin user management (403 Forbidden)
        var adminRes = await mgrClient.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, adminRes.StatusCode);
    }

    [Fact]
    public async Task Admin_CanAccessAdminEndpoints_AndManageRoles()
    {
        var adminClient = _factory.CreateAdminClient();

        // 1. Admin CAN list users
        var usersRes = await adminClient.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.OK, usersRes.StatusCode);
        var users = await usersRes.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotNull(users);

        // 2. Create a target user
        var targetEmail = $"target_{Guid.NewGuid():N}@test.local";
        var regRes = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = targetEmail,
            Password = "Password123!"
        });
        var targetUser = await regRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(targetUser);

        // 3. Admin updates roles for target user
        var updateRolesRes = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{targetUser.UserId}/roles",
            new UpdateUserRolesRequest { Roles = [Roles.InventoryManager] });

        Assert.Equal(HttpStatusCode.OK, updateRolesRes.StatusCode);
        var updated = await updateRolesRes.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(updated);
        Assert.Contains(Roles.InventoryManager, updated.Roles);
        Assert.DoesNotContain(Roles.Viewer, updated.Roles);
    }

    [Fact]
    public async Task Admin_CannotAssignInvalidRole_Returns400()
    {
        var adminClient = _factory.CreateAdminClient();

        var regRes = await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = $"role_val_{Guid.NewGuid():N}@test.local",
            Password = "Password123!"
        });
        var user = await regRes.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(user);

        var response = await adminClient.PutAsJsonAsync(
            $"/api/admin/users/{user.UserId}/roles",
            new UpdateUserRolesRequest { Roles = ["SuperAdmin", "HackerRole"] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("invalid", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_CannotRemoveAdminRoleFromLastActiveAdmin_Returns400()
    {
        var adminClient = _factory.CreateAdminClient();

        // Find the seeded admin user
        var usersRes = await adminClient.GetAsync("/api/admin/users");
        var users = await usersRes.Content.ReadFromJsonAsync<List<UserDto>>();
        Assert.NotNull(users);

        var adminUser = users.FirstOrDefault(u => u.Roles.Contains(Roles.Admin));
        if (adminUser != null)
        {
            // Attempt to demote the admin to Viewer
            var demoteRes = await adminClient.PutAsJsonAsync(
                $"/api/admin/users/{adminUser.Id}/roles",
                new UpdateUserRolesRequest { Roles = [Roles.Viewer] });

            Assert.Equal(HttpStatusCode.BadRequest, demoteRes.StatusCode);
            var problem = await demoteRes.Content.ReadFromJsonAsync<ProblemDetails>();
            Assert.NotNull(problem);
            Assert.Contains("last active administrator", problem.Detail, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task RateLimiting_ExceedingAuthRateLimit_Returns429TooManyRequests()
    {
        var rateLimitClient = _factory.CreateClient();
        rateLimitClient.DefaultRequestHeaders.UserAgent.ParseAdd("RateLimitExceedTestClient");

        var statuses = new List<HttpStatusCode>();

        for (int i = 0; i < 105; i++)
        {
            var res = await rateLimitClient.PostAsJsonAsync("/api/auth/login", new LoginRequest
            {
                Email = "rate_limit_test@stockroom.local",
                Password = "DummyPassword123!"
            });
            statuses.Add(res.StatusCode);
            if (res.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }
}
