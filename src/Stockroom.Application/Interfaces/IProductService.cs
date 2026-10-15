using Stockroom.Application.DTOs.Common;
using Stockroom.Application.DTOs.Products;

namespace Stockroom.Application.Interfaces;

public interface IProductService
{
    Task<ProductResponse> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductResponse?> GetProductByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<PagedResponse<ProductResponse>> GetProductsAsync(
        int page,
        int pageSize,
        string? search,
        bool? lowStockOnly,
        CancellationToken cancellationToken = default);
    Task<ProductResponse> UpdateProductAsync(int id, UpdateProductRequest request, CancellationToken cancellationToken = default);
    Task DeleteProductAsync(int id, CancellationToken cancellationToken = default);
}
