using Microsoft.Extensions.DependencyInjection;
using Stockroom.Application.Interfaces;
using Stockroom.Application.Services;

namespace Stockroom.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IInventoryService, InventoryService>();
        return services;
    }
}
