using Stockroom.Application.DTOs.Common;
using Stockroom.Application.DTOs.Inventory;
using Stockroom.Application.DTOs.Products;
using Stockroom.Application.DTOs.StockMovements;

namespace Stockroom.Application.Interfaces;

public interface IInventoryService
{
    Task<StockAdjustmentResult> StockInAsync(int productId, StockAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<StockAdjustmentResult> StockOutAsync(int productId, StockAdjustmentRequest request, CancellationToken cancellationToken = default);
    Task<PagedResponse<StockMovementResponse>> GetProductMovementsAsync(
        int productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductResponse>> GetLowStockProductsAsync(CancellationToken cancellationToken = default);
    Task<InventorySummaryResponse> GetInventorySummaryAsync(CancellationToken cancellationToken = default);
}
