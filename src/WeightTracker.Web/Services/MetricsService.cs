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

public enum GoalDirection
{
    None,
    Loss,
    Gain,
    Maintenance
}

public enum DirectionalStatus
{
    Unknown,
    Neutral,
    TowardGoal,
    AwayFromGoal
}

public enum GoalForecastStatus
{
    NoGoal,
    NoLatestWeight,
    AtGoal,
    NeedMoreData,
    PaceTooFlat,
    MovingAwayFromGoal,
    Estimated
}

public sealed record GoalForecast(
    GoalForecastStatus Status,
    DateOnly? EstimatedDate,
    string? SourceWindow,
    decimal? DailyPaceKg,
    int? DaysToGoal);

public sealed record GoalProgressRecord(
    int? WindowDays,
    decimal ChangeKg,
    DateOnly StartDate,
    DateOnly EndDate);

public sealed record GoalProgressInsights(
    GoalDirection GoalDirection,
    DirectionalStatus WeekOverWeekStatus,
    DirectionalStatus ThirtyDayStatus,
    DirectionalStatus NinetyDayStatus,
    GoalForecast Forecast,
    IReadOnlyList<GoalProgressRecord> Records);

public sealed class MetricsService
{
    private const decimal MaintenanceToleranceKg = 0.05m;
    private const decimal MinimumProjectionPaceKgPerDay = 0.01m;
    private const int MaximumProjectionDays = 730;
    private const int MinimumAllTimeProjectionEntries = 3;
    private const int MinimumAllTimeProjectionDays = 15;

    public GoalProgressInsights BuildMotivationalInsights(
        IEnumerable<WeightEntry> source,
        DateOnly today,
        DayOfWeek weekStartsOn,
        decimal? goalWeightKg)
    {
        var entries = source
            .Where(item => item.EntryDate <= today)
            .OrderBy(item => item.EntryDate)
            .ToList();

        decimal? latestWeightKg = entries.Count == 0 ? null : entries[^1].WeightKg;
        var direction = DetermineGoalDirection(latestWeightKg, goalWeightKg);
        var summary = BuildSummary(entries, today, weekStartsOn, goalWeightKg);

        return new GoalProgressInsights(
            direction,
            ClassifyChange(summary.WeekOverWeekDeltaKg, direction),
            ClassifyChange(summary.ThirtyDayChangeKg, direction),
            ClassifyChange(summary.NinetyDayChangeKg, direction),
            BuildGoalForecast(entries, goalWeightKg, direction),
            BuildGoalProgressRecords(entries, direction));
    }

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

    private static GoalDirection DetermineGoalDirection(decimal? latestWeightKg, decimal? goalWeightKg)
    {
        if (!latestWeightKg.HasValue || !goalWeightKg.HasValue)
        {
            return GoalDirection.None;
        }

        var difference = latestWeightKg.Value - goalWeightKg.Value;
        if (decimal.Abs(difference) <= MaintenanceToleranceKg)
        {
            return GoalDirection.Maintenance;
        }

        return difference > 0 ? GoalDirection.Loss : GoalDirection.Gain;
    }

    private static DirectionalStatus ClassifyChange(decimal? changeKg, GoalDirection direction)
    {
        if (!changeKg.HasValue || direction == GoalDirection.None)
        {
            return DirectionalStatus.Unknown;
        }

        if (decimal.Abs(changeKg.Value) <= MaintenanceToleranceKg)
        {
            return DirectionalStatus.Neutral;
        }

        return direction switch
        {
            GoalDirection.Loss => changeKg.Value < 0
                ? DirectionalStatus.TowardGoal
                : DirectionalStatus.AwayFromGoal,
            GoalDirection.Gain => changeKg.Value > 0
                ? DirectionalStatus.TowardGoal
                : DirectionalStatus.AwayFromGoal,
            GoalDirection.Maintenance => DirectionalStatus.Neutral,
            _ => DirectionalStatus.Unknown
        };
    }

    private static GoalForecast BuildGoalForecast(
        IReadOnlyList<WeightEntry> entries,
        decimal? goalWeightKg,
        GoalDirection direction)
    {
        if (!goalWeightKg.HasValue)
        {
            return new GoalForecast(GoalForecastStatus.NoGoal, null, null, null, null);
        }

        if (entries.Count == 0)
        {
            return new GoalForecast(GoalForecastStatus.NoLatestWeight, null, null, null, null);
        }

        if (direction == GoalDirection.None)
        {
            return new GoalForecast(GoalForecastStatus.NoGoal, null, null, null, null);
        }

        var latest = entries[^1];
        if (direction == GoalDirection.Maintenance)
        {
            return new GoalForecast(GoalForecastStatus.AtGoal, latest.EntryDate, null, 0m, 0);
        }

        var sawAwayFromGoal = false;
        var sawFlatPace = false;
        var candidates = new (int? WindowDays, string SourceWindow)[]
        {
            (30, "30-day"),
            (90, "90-day"),
            (null, "all-time")
        };

        foreach (var candidate in candidates)
        {
            var baseline = candidate.WindowDays.HasValue
                ? FindBaseline(entries, latest.EntryDate, candidate.WindowDays.Value)
                : entries[0];

            if (baseline is null)
            {
                continue;
            }

            var elapsedDays = latest.EntryDate.DayNumber - baseline.EntryDate.DayNumber;
            if (elapsedDays <= 0 || !HasEnoughForecastEvidence(candidate.WindowDays, elapsedDays, entries.Count))
            {
                continue;
            }

            var changeKg = latest.WeightKg - baseline.WeightKg;
            var dailyPaceKg = changeKg / elapsedDays;
            var status = ClassifyChange(changeKg, direction);

            if (status == DirectionalStatus.AwayFromGoal)
            {
                sawAwayFromGoal = true;
                continue;
            }

            if (status != DirectionalStatus.TowardGoal
                || decimal.Abs(dailyPaceKg) < MinimumProjectionPaceKgPerDay)
            {
                sawFlatPace = true;
                continue;
            }

            var remainingKg = decimal.Abs(latest.WeightKg - goalWeightKg.Value);
            var daysToGoal = (int)decimal.Ceiling(remainingKg / decimal.Abs(dailyPaceKg));
            if (daysToGoal < 0 || daysToGoal > MaximumProjectionDays)
            {
                sawFlatPace = true;
                continue;
            }

            return new GoalForecast(
                GoalForecastStatus.Estimated,
                latest.EntryDate.AddDays(daysToGoal),
                candidate.SourceWindow,
                decimal.Round(dailyPaceKg, 3),
                daysToGoal);
        }

        if (sawAwayFromGoal)
        {
            return new GoalForecast(GoalForecastStatus.MovingAwayFromGoal, null, null, null, null);
        }

        if (sawFlatPace)
        {
            return new GoalForecast(GoalForecastStatus.PaceTooFlat, null, null, null, null);
        }

        return new GoalForecast(GoalForecastStatus.NeedMoreData, null, null, null, null);
    }

    private static WeightEntry? FindBaseline(
        IReadOnlyList<WeightEntry> entries,
        DateOnly latestDate,
        int windowDays)
    {
        var targetDate = latestDate.AddDays(-windowDays);
        return entries.LastOrDefault(item => item.EntryDate <= targetDate)
            ?? entries.FirstOrDefault(item => item.EntryDate > targetDate && item.EntryDate < latestDate);
    }

    private static bool HasEnoughForecastEvidence(int? windowDays, int elapsedDays, int entryCount)
    {
        if (!windowDays.HasValue)
        {
            return entryCount >= MinimumAllTimeProjectionEntries
                && elapsedDays >= MinimumAllTimeProjectionDays;
        }

        return elapsedDays >= windowDays.Value / 2;
    }

    private static IReadOnlyList<GoalProgressRecord> BuildGoalProgressRecords(
        IReadOnlyList<WeightEntry> entries,
        GoalDirection direction)
    {
        if (entries.Count < 2 || direction is GoalDirection.None or GoalDirection.Maintenance)
        {
            return [];
        }

        var records = new List<GoalProgressRecord>();
        int?[] windows = [7, 30, 90, null];

        foreach (var windowDays in windows)
        {
            GoalProgressRecord? bestRecord = null;
            var bestProgressKg = 0m;

            for (var startIndex = 0; startIndex < entries.Count - 1; startIndex++)
            {
                for (var endIndex = startIndex + 1; endIndex < entries.Count; endIndex++)
                {
                    var start = entries[startIndex];
                    var end = entries[endIndex];
                    var elapsedDays = end.EntryDate.DayNumber - start.EntryDate.DayNumber;
                    if (elapsedDays <= 0 || (windowDays.HasValue && elapsedDays > windowDays.Value))
                    {
                        continue;
                    }

                    var changeKg = end.WeightKg - start.WeightKg;
                    var progressKg = direction == GoalDirection.Loss ? -changeKg : changeKg;
                    if (progressKg <= 0m || progressKg <= bestProgressKg)
                    {
                        continue;
                    }

                    bestProgressKg = progressKg;
                    bestRecord = new GoalProgressRecord(windowDays, changeKg, start.EntryDate, end.EntryDate);
                }
            }

            if (bestRecord is not null)
            {
                records.Add(bestRecord);
            }
        }

        return records;
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
