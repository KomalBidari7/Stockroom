using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Stockroom.Application.Interfaces;
using Stockroom.Infrastructure.Data;
using Stockroom.Infrastructure.Repositories;

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

        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IStockMovementRepository, StockMovementRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
