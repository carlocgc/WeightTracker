using WeightTracker.Web.Models;

namespace WeightTracker.Web.Services;

public sealed record MetricPoint(DateOnly Date, decimal WeightKg);

public sealed record MetricsSummary(
    decimal? LatestWeightKg,
    decimal? CurrentWeekAverageKg,
    decimal? PreviousWeekAverageKg,
    decimal? WeekOverWeekDeltaKg,
    decimal? SevenDayMovingAverageKg,
    decimal? ThirtyDayChangeKg,
    decimal? NinetyDayChangeKg,
    decimal? RangeHighKg,
    decimal? RangeLowKg,
    decimal? GoalWeightKg);

public sealed record ChartSeries(
    IReadOnlyList<MetricPoint> DailyWeights,
    IReadOnlyList<MetricPoint> WeeklyAverages,
    IReadOnlyList<MetricPoint> MovingAverages,
    decimal? GoalWeightKg);

public sealed class MetricsService
{
    public MetricsSummary BuildSummary(
        IEnumerable<WeightEntry> source,
        DateOnly today,
        DayOfWeek weekStartsOn,
        decimal? goalWeightKg)
    {
        var entries = source.OrderBy(item => item.EntryDate).ToList();
        if (entries.Count == 0)
        {
            return new MetricsSummary(null, null, null, null, null, null, null, null, null, goalWeightKg);
        }

        var currentWeekStart = StartOfWeek(today, weekStartsOn);
        var previousWeekStart = currentWeekStart.AddDays(-7);
        var previousWeekEnd = currentWeekStart.AddDays(-1);
        var currentWeekAverage = AverageForRange(entries, currentWeekStart, today);
        var previousWeekAverage = AverageForRange(entries, previousWeekStart, previousWeekEnd);
        decimal? weekDelta = currentWeekAverage.HasValue && previousWeekAverage.HasValue
            ? currentWeekAverage.Value - previousWeekAverage.Value
            : null;

        return new MetricsSummary(
            LatestWeightKg: entries[^1].WeightKg,
            CurrentWeekAverageKg: currentWeekAverage,
            PreviousWeekAverageKg: previousWeekAverage,
            WeekOverWeekDeltaKg: weekDelta,
            SevenDayMovingAverageKg: AverageForRange(entries, today.AddDays(-7), today),
            ThirtyDayChangeKg: ChangeSince(entries, today.AddDays(-30)),
            NinetyDayChangeKg: ChangeSince(entries, today.AddDays(-90)),
            RangeHighKg: entries.Max(item => item.WeightKg),
            RangeLowKg: entries.Min(item => item.WeightKg),
            GoalWeightKg: goalWeightKg);
    }

    public ChartSeries BuildChartSeries(
        IEnumerable<WeightEntry> source,
        DayOfWeek weekStartsOn,
        decimal? goalWeightKg)
    {
        var entries = source.OrderBy(item => item.EntryDate).ToList();
        var daily = entries
            .Select(item => new MetricPoint(item.EntryDate, item.WeightKg))
            .ToList();

        var weekly = entries
            .GroupBy(item => StartOfWeek(item.EntryDate, weekStartsOn))
            .OrderBy(group => group.Key)
            .Select(group => new MetricPoint(group.Key, decimal.Round(group.Average(item => item.WeightKg), 3)))
            .ToList();

        var moving = entries
            .Select(item => new MetricPoint(
                item.EntryDate,
                AverageForRange(entries, item.EntryDate.AddDays(-6), item.EntryDate)!.Value))
            .ToList();

        return new ChartSeries(daily, weekly, moving, goalWeightKg);
    }

    private static DateOnly StartOfWeek(DateOnly date, DayOfWeek weekStartsOn)
    {
        var diff = (7 + date.DayOfWeek - weekStartsOn) % 7;
        return date.AddDays(-diff);
    }

    private static decimal? AverageForRange(IReadOnlyCollection<WeightEntry> entries, DateOnly start, DateOnly end)
    {
        var values = entries
            .Where(item => item.EntryDate >= start && item.EntryDate <= end)
            .Select(item => item.WeightKg)
            .ToList();

        return values.Count == 0 ? null : decimal.Round(values.Average(), 3);
    }

    private static decimal? ChangeSince(IReadOnlyList<WeightEntry> entries, DateOnly since)
    {
        var latest = entries[^1];
        var baseline = entries.LastOrDefault(item => item.EntryDate <= since)
            ?? entries.FirstOrDefault(item => item.EntryDate >= since);

        return baseline is null ? null : latest.WeightKg - baseline.WeightKg;
    }
}
