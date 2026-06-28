using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;
using WeightTracker.Web.Models;

namespace WeightTracker.Web.Services;

public sealed class WeightEntryService(
    WeightTrackerDbContext db,
    SettingsService settingsService,
    ILocalDateProvider localDateProvider,
    IClock clock)
{
    public async Task<WeightEntry> SaveAsync(
        DateOnly entryDate,
        decimal weight,
        CancellationToken cancellationToken = default)
    {
        var today = await localDateProvider.GetTodayAsync(cancellationToken);
        if (entryDate > today)
        {
            throw new ArgumentOutOfRangeException(nameof(entryDate), "Weight entries cannot be in the future.");
        }

        var settings = await settingsService.GetAsync(cancellationToken);
        var weightKg = decimal.Round(WeightConversionService.ToKilograms(weight, settings.DisplayUnit), 3);
        var now = clock.UtcNow;
        var entry = await db.WeightEntries.SingleOrDefaultAsync(
            item => item.EntryDate == entryDate,
            cancellationToken);

        if (entry is null)
        {
            entry = new WeightEntry
            {
                EntryDate = entryDate,
                CreatedAtUtc = now,
            };
            db.WeightEntries.Add(entry);
        }

        entry.WeightKg = weightKg;
        entry.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken);

        return entry;
    }

    public Task<WeightEntry?> GetByDateAsync(
        DateOnly entryDate,
        CancellationToken cancellationToken = default) =>
        db.WeightEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.EntryDate == entryDate, cancellationToken);

    public Task<List<WeightEntry>> GetRangeAsync(
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default) =>
        db.WeightEntries
            .AsNoTracking()
            .Where(item => item.EntryDate >= start && item.EntryDate <= end)
            .OrderBy(item => item.EntryDate)
            .ToListAsync(cancellationToken);

    public async Task<bool> DeletePastAsync(
        DateOnly entryDate,
        CancellationToken cancellationToken = default)
    {
        var today = await localDateProvider.GetTodayAsync(cancellationToken);
        if (entryDate >= today)
        {
            throw new ArgumentOutOfRangeException(nameof(entryDate), "Only past weight entries can be deleted.");
        }

        var entry = await db.WeightEntries.SingleOrDefaultAsync(
            item => item.EntryDate == entryDate,
            cancellationToken);
        if (entry is null)
        {
            return false;
        }

        db.WeightEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }
}
