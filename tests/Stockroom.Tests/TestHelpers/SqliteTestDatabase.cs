using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Stockroom.Infrastructure.Data;

namespace Stockroom.Tests.TestHelpers;

public class SqliteTestDatabase : IDisposable
{
    public SqliteConnection Connection { get; }
    public StockroomDbContext Context { get; }

    public SqliteTestDatabase()
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var options = new DbContextOptionsBuilder<StockroomDbContext>()
            .UseSqlite(Connection)
            .Options;

        Context = new StockroomDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        Connection.Dispose();
    }
}
