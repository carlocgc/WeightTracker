using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using WeightTracker.Web.Data;

namespace WeightTracker.Tests;

public sealed class DatabaseStartupTests
{
    [Fact]
    public void NormalizeSqliteConnectionString_ResolvesRelativeDataSourceUnderContentRoot()
    {
        var contentRoot = Path.Combine(AppContext.BaseDirectory, "content-root");

        var connectionString = DatabaseInitializer.NormalizeSqliteConnectionString(
            "Data Source=App_Data/weighttracker.db",
            contentRoot);
        var builder = new SqliteConnectionStringBuilder(connectionString);

        Assert.Equal(Path.Combine(contentRoot, "App_Data", "weighttracker.db"), builder.DataSource);
    }

    [Fact]
    public async Task GetRoot_CreatesSqliteDirectoryAndSchema_WhenDatabasePathIsMissing()
    {
        var root = Path.Combine(
            AppContext.BaseDirectory,
            "startup-db-tests",
            Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(root, "nested", "weighttracker.db");
        var dataProtectionKeysPath = Path.Combine(
            Path.GetTempPath(),
            "weighttracker-tests",
            Guid.NewGuid().ToString("N"),
            "DataProtectionKeys");

        try
        {
            await using var app = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureLogging(logging => logging.ClearProviders());
                    builder.UseSetting("ConnectionStrings:WeightTracker", $"Data Source={databasePath}");
                    builder.UseSetting("DataProtection:KeysPath", dataProtectionKeysPath);
                });
            var client = app.CreateClient();

            var response = await client.GetAsync("/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(File.Exists(databasePath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}