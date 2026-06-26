using WeightTracker.Web.Models;
using WeightTracker.Web.Services;

namespace WeightTracker.Tests;

public sealed class MetricsServiceTests
{
    private readonly MetricsService _service = new();

    [Fact]
    public void BuildSummary_ComparesCurrentAndPreviousWeeklyAverages()
    {
        var entries = new[]
        {
            Entry("2026-06-15", 84.0m),
            Entry("2026-06-16", 83.0m),
            Entry("2026-06-22", 82.0m),
            Entry("2026-06-23", 81.0m)
        };

        var summary = _service.BuildSummary(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, null);

        Assert.Equal(81.5m, summary.CurrentWeekAverageKg);
        Assert.Equal(83.5m, summary.PreviousWeekAverageKg);
        Assert.Equal(-2.0m, summary.WeekOverWeekDeltaKg);
    }

    [Fact]
    public void BuildSummary_UsesRecordedEntriesOnlyForMovingAverage()
    {
        var entries = new[]
        {
            Entry("2026-06-18", 82.0m),
            Entry("2026-06-22", 81.0m),
            Entry("2026-06-25", 80.0m)
        };

        var summary = _service.BuildSummary(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, null);

        Assert.Equal(81.0m, summary.SevenDayMovingAverageKg);
    }

    [Fact]
    public void BuildSummary_ReturnsGoalAndRangeMetrics()
    {
        var entries = new[]
        {
            Entry("2026-05-26", 84.0m),
            Entry("2026-06-01", 83.0m),
            Entry("2026-06-25", 80.0m)
        };

        var summary = _service.BuildSummary(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, 75.0m);

        Assert.Equal(80.0m, summary.LatestWeightKg);
        Assert.Equal(-4.0m, summary.ThirtyDayChangeKg);
        Assert.Equal(84.0m, summary.RangeHighKg);
        Assert.Equal(80.0m, summary.RangeLowKg);
        Assert.Equal(75.0m, summary.GoalWeightKg);
    }

    [Fact]
    public void BuildChartSeries_ReturnsDailyWeeklyAndMovingAveragePoints()
    {
        var entries = new[]
        {
            Entry("2026-06-15", 84.0m),
            Entry("2026-06-16", 83.0m),
            Entry("2026-06-22", 82.0m),
            Entry("2026-06-23", 81.0m)
        };

        var series = _service.BuildChartSeries(entries, DayOfWeek.Monday, null);

        Assert.Equal(4, series.DailyWeights.Count);
        Assert.Equal(2, series.WeeklyAverages.Count);
        Assert.Equal(83.5m, series.WeeklyAverages[0].WeightKg);
        Assert.Equal(81.5m, series.WeeklyAverages[1].WeightKg);
        Assert.Equal(83.5m, series.MovingAverages[1].WeightKg);
    }

    private static WeightEntry Entry(string date, decimal weightKg)
    {
        return new WeightEntry
        {
            EntryDate = DateOnly.Parse(date),
            WeightKg = weightKg,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
