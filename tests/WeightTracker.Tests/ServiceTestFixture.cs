using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;

namespace WeightTracker.Tests;

public sealed class ServiceTestFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public ServiceTestFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    public WeightTrackerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WeightTrackerDbContext>()
            .UseSqlite(_connection)
            .Options;
        var db = new WeightTrackerDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
