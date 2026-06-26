using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class WeightEntryServiceTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 6, 26, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SaveAsync_InsertsAndUpdatesTheSingleEntryForTheCardDate()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db, "kg");
        var entryDate = new DateOnly(2026, 6, 25);

        await service.SaveAsync(entryDate, 82.5m);
        await service.SaveAsync(entryDate, 82.1m);

        var entries = await db.WeightEntries.AsNoTracking().ToListAsync();

        var entry = Assert.Single(entries);
        Assert.Equal(entryDate, entry.EntryDate);
        Assert.Equal(82.1m, entry.WeightKg);
        Assert.Equal(FixedUtcNow, entry.CreatedAtUtc);
        Assert.Equal(FixedUtcNow, entry.UpdatedAtUtc);
    }

    [Fact]
    public async Task SaveAsync_UsesTheConfiguredDisplayUnit()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db, "lb");

        await service.SaveAsync(new DateOnly(2026, 6, 25), 200m);

        var entry = await db.WeightEntries.SingleAsync();

        Assert.Equal(90.718m, entry.WeightKg);
    }

    [Fact]
    public async Task SaveAsync_RejectsFutureCardDate()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db, "kg");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.SaveAsync(new DateOnly(2026, 6, 27), 82m));
    }

    [Fact]
    public async Task GetRangeAsync_ReturnsOnlyTheInclusiveRangeInDateOrder()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db, "kg");

        await service.SaveAsync(new DateOnly(2026, 6, 24), 81m);
        await service.SaveAsync(new DateOnly(2026, 6, 25), 82m);

        var entries = await service.GetRangeAsync(
            new DateOnly(2026, 6, 25),
            new DateOnly(2026, 6, 26));

        var entry = Assert.Single(entries);
        Assert.Equal(new DateOnly(2026, 6, 25), entry.EntryDate);
    }

    [Fact]
    public async Task DeletePastAsync_DeletesPastEntryAndRejectsToday()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db, "kg");
        var pastDate = new DateOnly(2026, 6, 25);

        await service.SaveAsync(pastDate, 82m);

        var deleted = await service.DeletePastAsync(pastDate);

        Assert.True(deleted);
        Assert.Empty(await db.WeightEntries.ToListAsync());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.DeletePastAsync(new DateOnly(2026, 6, 26)));
    }

    private static async Task<WeightEntryService> CreateServiceAsync(
        WeightTrackerDbContext db,
        string displayUnit)
    {
        var settings = new SettingsService(db);
        await settings.UpdateAsync(displayUnit, null, DayOfWeek.Monday, "Europe/London", "dark");
        var clock = new FixedClock(FixedUtcNow);
        var localDateProvider = new LocalDateProvider(settings, clock);

        return new WeightEntryService(db, settings, localDateProvider, clock);
    }
}
