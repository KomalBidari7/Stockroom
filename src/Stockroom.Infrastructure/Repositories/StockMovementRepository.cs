using Microsoft.EntityFrameworkCore;
using Stockroom.Application.Interfaces;
using Stockroom.Domain.Entities;
using Stockroom.Infrastructure.Data;

namespace Stockroom.Infrastructure.Repositories;

public class StockMovementRepository : IStockMovementRepository
{
    private readonly StockroomDbContext _context;

    public StockMovementRepository(StockroomDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(StockMovement movement, CancellationToken cancellationToken = default)
    {
        await _context.StockMovements.AddAsync(movement, cancellationToken);
    }

    public async Task<(IReadOnlyList<StockMovement> Items, int TotalCount)> GetPagedByProductIdAsync(
        int productId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _context.StockMovements
            .AsNoTracking()
            .Where(m => m.ProductId == productId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> HasMovementsAsync(int productId, CancellationToken cancellationToken = default)
    {
        return await _context.StockMovements
            .AnyAsync(m => m.ProductId == productId, cancellationToken);
    }
}
