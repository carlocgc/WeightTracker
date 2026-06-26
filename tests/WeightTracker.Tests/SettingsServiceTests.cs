using WeightTracker.Web.Models;
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task GetAsync_CreatesDarkDefaultSettings()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();

        var service = new SettingsService(db);

        var settings = await service.GetAsync();

        Assert.Equal(AppSettings.SingletonId, settings.Id);
        Assert.Equal("kg", settings.DisplayUnit);
        Assert.Equal("dark", settings.Theme);
        Assert.Equal(DayOfWeek.Monday, settings.WeekStartsOn);
    }

    [Fact]
    public async Task UpdateAsync_PersistsThemeAndGoal()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();

        var service = new SettingsService(db);

        await service.UpdateAsync("lb", 190m, DayOfWeek.Sunday, "Europe/London", "light");
        var settings = await service.GetAsync();

        Assert.Equal("lb", settings.DisplayUnit);
        Assert.Equal(190m, settings.GoalWeightKg);
        Assert.Equal(DayOfWeek.Sunday, settings.WeekStartsOn);
        Assert.Equal("Europe/London", settings.TimeZoneId);
        Assert.Equal("light", settings.Theme);
    }
}
