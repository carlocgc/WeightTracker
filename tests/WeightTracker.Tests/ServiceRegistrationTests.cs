using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class ServiceRegistrationTests
{
    [Fact]
    public void ConfigureServices_ResolvesEntryServices()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var scope = factory.Services.CreateScope();

        Assert.IsType<SystemClock>(scope.ServiceProvider.GetRequiredService<IClock>());
        Assert.IsType<LocalDateProvider>(scope.ServiceProvider.GetRequiredService<ILocalDateProvider>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<WeightEntryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<MetricsService>());
    }
}
