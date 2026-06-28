using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Models;

namespace WeightTracker.Web.Data;

public static class DatabaseInitializer
{
    public static string NormalizeSqliteConnectionString(string connectionString, string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        {
            return connectionString;
        }

        if (!Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, builder.DataSource));
        }

        return builder.ConnectionString;
    }

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WeightTrackerDbContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();

        EnsureSqliteDirectory(db, environment);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.AppSettings.AnyAsync(settings => settings.Id == AppSettings.SingletonId, cancellationToken))
        {
            db.AppSettings.Add(new AppSettings());
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static void EnsureSqliteDirectory(WeightTrackerDbContext db, IWebHostEnvironment environment)
    {
        var connectionString = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var databasePath = Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.Combine(environment.ContentRootPath, dataSource);
        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(databaseDirectory))
        {
            Directory.CreateDirectory(databaseDirectory);
        }
    }
}
