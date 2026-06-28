using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using WeightTracker.Web.Data;
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public void ConfigureServices_ResolvesEntryServices()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var dataProtectionKeysPath = Path.Combine(
            Path.GetTempPath(),
            "weighttracker-tests",
            Guid.NewGuid().ToString("N"),
            "DataProtectionKeys");

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("DataProtection:KeysPath", dataProtectionKeysPath);
                builder.ConfigureLogging(logging => logging.ClearProviders());
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<DbContextOptions<WeightTrackerDbContext>>();
                    services.AddDbContext<WeightTrackerDbContext>(options => options.UseSqlite(connection));
                });
            });
        using var scope = factory.Services.CreateScope();

        Assert.IsType<SystemClock>(scope.ServiceProvider.GetRequiredService<IClock>());
        Assert.IsType<LocalDateProvider>(scope.ServiceProvider.GetRequiredService<ILocalDateProvider>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<WeightEntryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MetricsService>());
    }
}