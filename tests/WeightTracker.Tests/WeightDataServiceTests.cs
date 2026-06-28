using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;
using WeightTracker.Web.Models;
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class WeightDataServiceTests
{
    private static readonly DateTime FixedUtcNow = new(2026, 6, 26, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ExportCsvAsync_WritesWeightEntriesInDateOrder()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        await AddEntryAsync(db, new DateOnly(2026, 6, 26), 82.1m, null);
        await AddEntryAsync(db, new DateOnly(2026, 6, 24), 83.125m, "first");
        var service = await CreateServiceAsync(db);

        var csv = await service.ExportCsvAsync();

        Assert.Equal(
            "entry_date,weight_kg,note\n2026-06-24,83.125,first\n2026-06-26,82.100,\n",
            csv);
    }

    [Fact]
    public async Task ExportCsvAsync_EscapesCsvNotes()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        await AddEntryAsync(db, new DateOnly(2026, 6, 24), 83.125m, "quoted \"note\", with comma\nand line");
        var service = await CreateServiceAsync(db);

        var csv = await service.ExportCsvAsync();

        Assert.Equal(
            "entry_date,weight_kg,note\n2026-06-24,83.125,\"quoted \"\"note\"\", with comma\nand line\"\n",
            csv);
    }

    [Fact]
    public async Task ImportCsvAsync_UpsertsEntriesByDate()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        await AddEntryAsync(db, new DateOnly(2026, 6, 24), 90m, "old");
        var service = await CreateServiceAsync(db);

        var result = await service.ImportCsvAsync(
            "entry_date,weight_kg,note\n2026-06-24,82.125,new note\n2026-06-25,81.000,\n");

        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Equal(1, result.InsertedCount);
        Assert.Equal(1, result.UpdatedCount);

        var entries = await db.WeightEntries.AsNoTracking().OrderBy(entry => entry.EntryDate).ToListAsync();
        Assert.Collection(
            entries,
            entry =>
            {
                Assert.Equal(new DateOnly(2026, 6, 24), entry.EntryDate);
                Assert.Equal(82.125m, entry.WeightKg);
                Assert.Equal("new note", entry.Note);
                Assert.Equal(FixedUtcNow, entry.CreatedAtUtc);
                Assert.Equal(FixedUtcNow, entry.UpdatedAtUtc);
            },
            entry =>
            {
                Assert.Equal(new DateOnly(2026, 6, 25), entry.EntryDate);
                Assert.Equal(81.000m, entry.WeightKg);
                Assert.Null(entry.Note);
                Assert.Equal(FixedUtcNow, entry.CreatedAtUtc);
                Assert.Equal(FixedUtcNow, entry.UpdatedAtUtc);
            });
    }

    [Fact]
    public async Task ImportCsvAsync_ParsesQuotedCsvNotes()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db);

        var result = await service.ImportCsvAsync(
            "entry_date,weight_kg,note\n2026-06-24,82.125,\"quoted \"\"note\"\", with comma\nand line\"\n");

        Assert.True(result.Success, string.Join("\n", result.Errors));
        var entry = await db.WeightEntries.SingleAsync();
        Assert.Equal("quoted \"note\", with comma\nand line", entry.Note);
    }

    [Fact]
    public async Task ImportCsvAsync_RejectsInvalidRowsWithoutPartialWrites()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        await AddEntryAsync(db, new DateOnly(2026, 6, 24), 90m, "old");
        var service = await CreateServiceAsync(db);

        var result = await service.ImportCsvAsync(
            "entry_date,weight_kg,note\n2026-06-24,82.125,new note\n2026-06-25,1000.001,invalid\n");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("Row 3", StringComparison.Ordinal));
        var entry = await db.WeightEntries.SingleAsync();
        Assert.Equal(new DateOnly(2026, 6, 24), entry.EntryDate);
        Assert.Equal(90m, entry.WeightKg);
        Assert.Equal("old", entry.Note);
    }

    [Fact]
    public async Task ImportCsvAsync_RejectsMissingRequiredHeaders()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db);

        var result = await service.ImportCsvAsync("entry_date,weight,note\n2026-06-24,82.125,\n");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("header", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await db.WeightEntries.ToListAsync());
    }

    [Fact]
    public async Task ImportCsvAsync_RejectsDuplicateDatesInsideCsv()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db);

        var result = await service.ImportCsvAsync(
            "entry_date,weight_kg,note\n2026-06-24,82.125,first\n2026-06-24,82.250,second\n");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await db.WeightEntries.ToListAsync());
    }

    [Fact]
    public async Task ImportCsvAsync_RejectsFutureDates()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db);

        var result = await service.ImportCsvAsync("entry_date,weight_kg,note\n2026-06-27,82.125,\n");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("future", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await db.WeightEntries.ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("0.099")]
    [InlineData("1000.001")]
    [InlineData("82.1234")]
    public async Task ImportCsvAsync_RejectsInvalidWeights(string weightKg)
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db);

        var result = await service.ImportCsvAsync($"entry_date,weight_kg,note\n2026-06-24,{weightKg},\n");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("weight_kg", StringComparison.Ordinal));
        Assert.Empty(await db.WeightEntries.ToListAsync());
    }

    [Fact]
    public async Task ImportCsvAsync_RejectsNotesOverFiveHundredCharacters()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db);
        var longNote = new string('x', 501);

        var result = await service.ImportCsvAsync($"entry_date,weight_kg,note\n2026-06-24,82.125,{longNote}\n");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("note", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await db.WeightEntries.ToListAsync());
    }

    [Fact]
    public async Task DeleteAllWeightsAsync_RequiresExactDeleteConfirmation()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        await AddEntryAsync(db, new DateOnly(2026, 6, 24), 82m, null);
        var service = await CreateServiceAsync(db);

        var result = await service.DeleteAllWeightsAsync("delete");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("DELETE", StringComparison.Ordinal));
        Assert.Single(await db.WeightEntries.ToListAsync());
    }

    [Fact]
    public async Task DeleteAllWeightsAsync_DeletesWeightEntriesAndPreservesSettings()
    {
        using var fixture = new ServiceTestFixture();
        await using var db = fixture.CreateDbContext();
        var service = await CreateServiceAsync(db);
        var settingsService = new SettingsService(db);
        await settingsService.UpdateAsync("lb", 75m, DayOfWeek.Sunday, "Europe/London", "light");
        await AddEntryAsync(db, new DateOnly(2026, 6, 24), 82m, null);
        await AddEntryAsync(db, new DateOnly(2026, 6, 25), 81m, null);

        var result = await service.DeleteAllWeightsAsync("DELETE");

        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.Equal(2, result.DeletedCount);
        Assert.Empty(await db.WeightEntries.ToListAsync());
        var settings = await db.AppSettings.AsNoTracking().SingleAsync(item => item.Id == AppSettings.SingletonId);
        Assert.Equal("lb", settings.DisplayUnit);
        Assert.Equal(75m, settings.GoalWeightKg);
        Assert.Equal(DayOfWeek.Sunday, settings.WeekStartsOn);
        Assert.Equal("Europe/London", settings.TimeZoneId);
        Assert.Equal("light", settings.Theme);
    }

    private static async Task AddEntryAsync(
        WeightTrackerDbContext db,
        DateOnly entryDate,
        decimal weightKg,
        string? note)
    {
        db.WeightEntries.Add(new WeightEntry
        {
            EntryDate = entryDate,
            WeightKg = weightKg,
            Note = note,
            CreatedAtUtc = FixedUtcNow,
            UpdatedAtUtc = FixedUtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<WeightDataService> CreateServiceAsync(WeightTrackerDbContext db)
    {
        var settings = new SettingsService(db);
        await settings.UpdateAsync("kg", null, DayOfWeek.Monday, "Europe/London", "dark");
        var clock = new FixedClock(FixedUtcNow);
        var localDateProvider = new LocalDateProvider(settings, clock);

        return new WeightDataService(db, localDateProvider, clock);
    }
}