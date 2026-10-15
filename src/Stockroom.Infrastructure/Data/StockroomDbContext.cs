using Microsoft.EntityFrameworkCore;
using Stockroom.Domain.Entities;

namespace Stockroom.Infrastructure.Data;

public class StockroomDbContext : DbContext
{
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public StockroomDbContext(DbContextOptions<StockroomDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StockroomDbContext).Assembly);
    }
}
