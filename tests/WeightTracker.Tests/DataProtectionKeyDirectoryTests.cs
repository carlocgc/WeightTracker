using Microsoft.Data.Sqlite;
using WeightTracker.Web.Data;

namespace WeightTracker.Tests;

public sealed class DataProtectionKeyDirectoryTests
{
    [Fact]
    public void Resolve_UsesConfiguredKeysPath()
    {
        var contentRoot = Path.Combine(AppContext.BaseDirectory, "content-root");
        var configuredPath = Path.Combine(AppContext.BaseDirectory, "configured-keys");

        var directory = DataProtectionKeyDirectory.Resolve(
            configuredPath,
            "Data Source=:memory:",
            contentRoot);

        Assert.Equal(configuredPath, directory.FullName);
    }

    [Fact]
    public void Resolve_UsesDirectoryBesideSqliteDatabase()
    {
        var databaseDirectory = Path.Combine(AppContext.BaseDirectory, "runtime-data");
        var databasePath = Path.Combine(databaseDirectory, "weighttracker.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath
        }.ConnectionString;

        var directory = DataProtectionKeyDirectory.Resolve(connectionString, AppContext.BaseDirectory);

        Assert.Equal(Path.Combine(databaseDirectory, "DataProtectionKeys"), directory.FullName);
    }

    [Fact]
    public void Resolve_UsesAppDataFallbackForInMemorySqlite()
    {
        var contentRoot = Path.Combine(AppContext.BaseDirectory, "content-root");

        var directory = DataProtectionKeyDirectory.Resolve("Data Source=:memory:", contentRoot);

        Assert.Equal(Path.Combine(contentRoot, "App_Data", "DataProtectionKeys"), directory.FullName);
    }
}
