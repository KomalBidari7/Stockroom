using Microsoft.EntityFrameworkCore;
using Stockroom.Application.Interfaces;

namespace Stockroom.Infrastructure.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly StockroomDbContext _context;

    public UnitOfWork(StockroomDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken cancellationToken = default)
    {
        // If already inside an existing transaction, simply execute the operation
        if (_context.Database.CurrentTransaction != null)
        {
            await operation();
            return;
        }

        // SQLite supports transactions via BeginTransactionAsync
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await operation();
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
