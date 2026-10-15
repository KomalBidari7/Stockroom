using Stockroom.Domain.Entities;

namespace Stockroom.Application.Interfaces;

public interface IStockMovementRepository
{
    Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<StockMovement> Items, int TotalCount)> GetPagedByProductIdAsync(
        int productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> HasMovementsAsync(int productId, CancellationToken cancellationToken = default);
}
