using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class LocalDateProviderTests
{
    [Fact]
    public async Task GetTodayAsync_ConvertsTheUtcClockUsingTheSavedTimeZone()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();

        var settings = new SettingsService(db);
        await settings.UpdateAsync("kg", null, DayOfWeek.Monday, "Europe/London", "dark");
        var provider = new LocalDateProvider(
            settings,
            new FixedClock(new DateTime(2026, 6, 25, 23, 30, 0, DateTimeKind.Utc)));

        var today = await provider.GetTodayAsync();

        Assert.Equal(new DateOnly(2026, 6, 26), today);
    }
}
