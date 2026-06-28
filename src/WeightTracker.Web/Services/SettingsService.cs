using Microsoft.EntityFrameworkCore;
using WeightTracker.Web.Data;
using WeightTracker.Web.Models;

namespace WeightTracker.Web.Services;

public sealed class SettingsService(WeightTrackerDbContext db)
{
    private static readonly HashSet<string> ValidUnits = ["kg", "lb"];
    private static readonly HashSet<string> ValidThemes = ["dark", "light", "system"];

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.AppSettings.FindAsync([AppSettings.SingletonId], cancellationToken);
        if (settings is not null)
        {
            return settings;
        }

        settings = new AppSettings();
        db.AppSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);

        return settings;
    }

    public async Task<AppSettings> UpdateAsync(
        string displayUnit,
        decimal? goalWeightKg,
        DayOfWeek weekStartsOn,
        string timeZoneId,
        string theme,
        CancellationToken cancellationToken = default)
    {
        displayUnit = WeightConversionService.NormalizeUnit(displayUnit);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(timeZoneId);
        theme = theme.Trim().ToLowerInvariant();

        if (!ValidUnits.Contains(displayUnit))
        {
            throw new ArgumentOutOfRangeException(nameof(displayUnit), "Supported units are kg and lb.");
        }

        if (!ValidThemes.Contains(theme))
        {
            throw new ArgumentOutOfRangeException(nameof(theme), "Supported themes are dark, light, and system.");
        }

        if (goalWeightKg is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(goalWeightKg), "Goal weight must be greater than zero.");
        }

        _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        var settings = await db.AppSettings.SingleOrDefaultAsync(
            item => item.Id == AppSettings.SingletonId,
            cancellationToken);

        if (settings is null)
        {
            settings = new AppSettings();
            db.AppSettings.Add(settings);
        }

        settings.DisplayUnit = displayUnit;
        settings.GoalWeightKg = goalWeightKg;
        settings.WeekStartsOn = weekStartsOn;
        settings.TimeZoneId = timeZoneId;
        settings.Theme = theme;

        await db.SaveChangesAsync(cancellationToken);

        return settings;
    }
}
