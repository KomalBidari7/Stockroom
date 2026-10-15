using Microsoft.EntityFrameworkCore;
using Stockroom.Application.Interfaces;
using Stockroom.Domain.Entities;
using Stockroom.Infrastructure.Data;

namespace Stockroom.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly StockroomDbContext _context;

    public ProductRepository(StockroomDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        return await _context.Products
            .FirstOrDefaultAsync(p => p.SKU == normalized, cancellationToken);
    }

    public async Task<bool> ExistsBySkuAsync(string sku, int? excludeProductId = null, CancellationToken cancellationToken = default)
    {
        var normalized = sku.Trim().ToUpperInvariant();
        return await _context.Products
            .AnyAsync(p => p.SKU == normalized && (!excludeProductId.HasValue || p.Id != excludeProductId.Value), cancellationToken);
    }

    public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        string? search,
        bool? lowStockOnly,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{term}%") ||
                EF.Functions.Like(p.SKU, $"%{term}%"));
        }

        if (lowStockOnly == true)
        {
            query = query.Where(p => p.LowStockThreshold > 0 && p.QuantityInStock <= p.LowStockThreshold);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Product>> GetLowStockProductsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.LowStockThreshold > 0 && p.QuantityInStock <= p.LowStockThreshold)
            .OrderBy(p => p.QuantityInStock)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllForSummaryAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
    }

    public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        _context.Products.Update(product);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Product product, CancellationToken cancellationToken = default)
    {
        _context.Products.Remove(product);
        return Task.CompletedTask;
    }
}
