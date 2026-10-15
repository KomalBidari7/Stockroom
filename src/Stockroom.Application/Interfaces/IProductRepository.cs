using Stockroom.Domain.Entities;

namespace Stockroom.Application.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<bool> ExistsBySkuAsync(string sku, int? excludeProductId = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? lowStockOnly,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetAllForSummaryAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);
    Task DeleteAsync(Product product, CancellationToken cancellationToken = default);
}
