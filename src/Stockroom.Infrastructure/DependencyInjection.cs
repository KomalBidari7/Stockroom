using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockroom.Application.Interfaces;
using Stockroom.Infrastructure.Authentication;
using Stockroom.Infrastructure.Data;
using Stockroom.Infrastructure.Identity;
using Stockroom.Infrastructure.Repositories;
using Stockroom.Infrastructure.Services;

namespace Stockroom.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContext<StockroomDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        // Use AddIdentityCore so cookie authentication schemes do not hijack the default JWT Bearer scheme
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireLowercase = false;
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole>()
        .AddRoleManager<RoleManager<IdentityRole>>()
        .AddUserManager<UserManager<ApplicationUser>>()
        .AddEntityFrameworkStores<StockroomDbContext>()
        .AddDefaultTokenProviders();

        // Repositories and Unit of Work
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Authentication & User Admin services
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdminService, UserAdminService>();

        return services;
    }
}
