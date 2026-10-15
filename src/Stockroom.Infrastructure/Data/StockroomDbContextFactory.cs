using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Stockroom.Infrastructure.Data;

public class StockroomDbContextFactory : IDesignTimeDbContextFactory<StockroomDbContext>
{
    public StockroomDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StockroomDbContext>();
        optionsBuilder.UseSqlite("Data Source=stockroom.db");

        return new StockroomDbContext(optionsBuilder.Options);
    }
}
