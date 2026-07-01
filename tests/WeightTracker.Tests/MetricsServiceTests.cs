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

    [Fact]
    public void BuildMotivationalInsights_ForLossGoal_ComputesForecastAndGoalDirectionRecords()
    {
        var entries = new[]
        {
            Entry("2026-05-26", 86.0m),
            Entry("2026-06-18", 84.0m),
            Entry("2026-06-25", 83.0m)
        };

        var insights = _service.BuildMotivationalInsights(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, 80.0m);

        Assert.Equal(GoalDirection.Loss, insights.GoalDirection);
        Assert.Equal(DirectionalStatus.TowardGoal, insights.ThirtyDayStatus);
        Assert.Equal(GoalForecastStatus.Estimated, insights.Forecast.Status);
        Assert.Equal(new DateOnly(2026, 7, 25), insights.Forecast.EstimatedDate);
        Assert.Equal("30-day", insights.Forecast.SourceWindow);

        var sevenDay = Assert.Single(insights.Records, record => record.WindowDays == 7);
        Assert.Equal(-1.0m, sevenDay.ChangeKg);
        Assert.Equal(new DateOnly(2026, 6, 18), sevenDay.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 25), sevenDay.EndDate);

        var thirtyDay = Assert.Single(insights.Records, record => record.WindowDays == 30);
        Assert.Equal(-3.0m, thirtyDay.ChangeKg);
        Assert.Equal(new DateOnly(2026, 5, 26), thirtyDay.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 25), thirtyDay.EndDate);
    }

    [Fact]
    public void BuildMotivationalInsights_ForGainGoal_TreatsWeightGainAsProgress()
    {
        var entries = new[]
        {
            Entry("2026-05-26", 80.0m),
            Entry("2026-06-18", 82.0m),
            Entry("2026-06-25", 83.0m)
        };

        var insights = _service.BuildMotivationalInsights(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, 86.0m);

        Assert.Equal(GoalDirection.Gain, insights.GoalDirection);
        Assert.Equal(DirectionalStatus.TowardGoal, insights.ThirtyDayStatus);
        Assert.Equal(GoalForecastStatus.Estimated, insights.Forecast.Status);
        Assert.Equal(new DateOnly(2026, 7, 25), insights.Forecast.EstimatedDate);

        var thirtyDay = Assert.Single(insights.Records, record => record.WindowDays == 30);
        Assert.Equal(3.0m, thirtyDay.ChangeKg);
        Assert.Equal(new DateOnly(2026, 5, 26), thirtyDay.StartDate);
        Assert.Equal(new DateOnly(2026, 6, 25), thirtyDay.EndDate);
    }

    [Fact]
    public void BuildMotivationalInsights_SuppressesForecastWhenMovingAwayFromGoal()
    {
        var entries = new[]
        {
            Entry("2026-05-26", 83.0m),
            Entry("2026-06-25", 84.0m)
        };

        var insights = _service.BuildMotivationalInsights(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, 80.0m);

        Assert.Equal(GoalDirection.Loss, insights.GoalDirection);
        Assert.Equal(DirectionalStatus.AwayFromGoal, insights.ThirtyDayStatus);
        Assert.Equal(GoalForecastStatus.MovingAwayFromGoal, insights.Forecast.Status);
        Assert.Null(insights.Forecast.EstimatedDate);
        Assert.Empty(insights.Records);
    }

    [Fact]
    public void BuildMotivationalInsights_WithSparseFiniteWindowData_ReturnsNeedMoreDataForecast()
    {
        var entries = new[]
        {
            Entry("2026-06-24", 84.0m),
            Entry("2026-06-25", 83.0m)
        };

        var insights = _service.BuildMotivationalInsights(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, 80.0m);

        Assert.Equal(GoalForecastStatus.NeedMoreData, insights.Forecast.Status);
        Assert.Null(insights.Forecast.EstimatedDate);
    }

    [Fact]
    public void BuildMotivationalInsights_WithMaintenanceGoalAfterProgress_ReturnsNeutralStatus()
    {
        var entries = new[]
        {
            Entry("2026-05-26", 83.0m),
            Entry("2026-06-25", 80.0m)
        };

        var insights = _service.BuildMotivationalInsights(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, 80.0m);

        Assert.Equal(GoalDirection.Maintenance, insights.GoalDirection);
        Assert.Equal(DirectionalStatus.Neutral, insights.ThirtyDayStatus);
        Assert.Equal(GoalForecastStatus.AtGoal, insights.Forecast.Status);
    }

    [Fact]
    public void BuildMotivationalInsights_WithNoGoal_ReturnsNeutralNoGoalState()
    {
        var entries = new[]
        {
            Entry("2026-06-18", 84.0m),
            Entry("2026-06-25", 83.0m)
        };

        var insights = _service.BuildMotivationalInsights(entries, new DateOnly(2026, 6, 25), DayOfWeek.Monday, null);

        Assert.Equal(GoalDirection.None, insights.GoalDirection);
        Assert.Equal(DirectionalStatus.Unknown, insights.ThirtyDayStatus);
        Assert.Equal(GoalForecastStatus.NoGoal, insights.Forecast.Status);
        Assert.Null(insights.Forecast.EstimatedDate);
        Assert.Empty(insights.Records);
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
