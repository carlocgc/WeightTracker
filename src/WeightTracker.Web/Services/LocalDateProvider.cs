namespace WeightTracker.Web.Services;

public interface ILocalDateProvider
{
    Task<DateOnly> GetTodayAsync(CancellationToken cancellationToken = default);
}

public sealed class LocalDateProvider(SettingsService settingsService, IClock clock) : ILocalDateProvider
{
    public async Task<DateOnly> GetTodayAsync(CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.GetAsync(cancellationToken);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        var localTime = TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, timeZone);

        return DateOnly.FromDateTime(localTime);
    }
}
