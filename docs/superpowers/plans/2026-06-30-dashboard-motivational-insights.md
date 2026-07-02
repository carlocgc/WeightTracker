# Dashboard Motivational Insights Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add goal-aware motivational dashboard insights: directional indicators, guarded goal forecast, goal-direction personal records, and the empty validation-strip fix.

**Architecture:** Keep the app as a Razor Pages dashboard. Put metric semantics in `MetricsService`, expose a `GoalProgressInsights` model through `IndexModel`, and keep Razor/CSS focused on rendering. No new persistence, routes, pages, modals, or third-party dependencies are needed.

**Tech Stack:** ASP.NET Core Razor Pages, C# records/enums, EF Core-backed page tests, xUnit, existing Chart.js only for the already-present trend chart.

---

## File Structure

- Modify `src/WeightTracker.Web/Services/MetricsService.cs`
  - Add goal-direction enums and records.
  - Add `BuildMotivationalInsights`.
  - Keep existing `BuildSummary` and `BuildChartSeries` public behavior intact.
- Modify `src/WeightTracker.Web/Pages/Index.cshtml.cs`
  - Add `ProgressInsights`.
  - Add formatting helpers for forecast, direction arrows, status classes, record labels, and record ranges.
- Modify `src/WeightTracker.Web/Pages/Index.cshtml`
  - Hide validation summary when model state is valid.
  - Rename the insight panel to Progress insights.
  - Render forecast, directional metric values, and goal-direction records.
- Modify `src/WeightTracker.Web/wwwroot/css/site.css`
  - Add compact directional value, forecast, and record styles.
  - Keep the existing dark dashboard visual system.
- Modify `tests/WeightTracker.Tests/MetricsServiceTests.cs`
  - Cover goal direction, directional statuses, forecast states, and records.
- Modify `tests/WeightTracker.Tests/DashboardPageTests.cs`
  - Cover rendered Progress insights and the hidden validation strip.

## Task 1: Add Failing Metrics Tests

**Files:**
- Modify: `tests/WeightTracker.Tests/MetricsServiceTests.cs`
- Test: `tests/WeightTracker.Tests/MetricsServiceTests.cs`

- [ ] **Step 1: Write failing tests for motivational metric semantics**

Add these tests before the private `Entry` helper:

```csharp
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
```

- [ ] **Step 2: Run the metric tests and verify they fail**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter MetricsServiceTests
```

Expected: FAIL because `BuildMotivationalInsights`, `GoalDirection`, `DirectionalStatus`, and `GoalForecastStatus` do not exist yet.

- [ ] **Step 3: Commit the failing tests**

```powershell
git add tests\WeightTracker.Tests\MetricsServiceTests.cs
git commit -m "test: add motivational insight metric coverage"
```

## Task 2: Implement Motivational Metrics

**Files:**
- Modify: `src/WeightTracker.Web/Services/MetricsService.cs`
- Test: `tests/WeightTracker.Tests/MetricsServiceTests.cs`

- [ ] **Step 1: Add the public metric model types**

In `MetricsService.cs`, add these declarations after `ChartSeries`:

```csharp
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
```

- [ ] **Step 2: Add constants and the public builder method**

Inside `MetricsService`, add these members before `BuildSummary`:

```csharp
private const decimal MaintenanceToleranceKg = 0.05m;
private const decimal MinimumProjectionPaceKgPerDay = 0.01m;
private const int MaximumProjectionDays = 730;

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

    var latestWeightKg = entries.Count == 0 ? null : entries[^1].WeightKg;
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
```

- [ ] **Step 3: Add goal direction and directional status helpers**

Inside `MetricsService`, add these private helpers before `StartOfWeek`:

```csharp
private static GoalDirection DetermineGoalDirection(decimal? latestWeightKg, decimal? goalWeightKg)
{
    if (!latestWeightKg.HasValue || !goalWeightKg.HasValue)
    {
        return GoalDirection.None;
    }

    var gap = latestWeightKg.Value - goalWeightKg.Value;
    if (Math.Abs(gap) <= MaintenanceToleranceKg)
    {
        return GoalDirection.Maintenance;
    }

    return gap > 0 ? GoalDirection.Loss : GoalDirection.Gain;
}

private static DirectionalStatus ClassifyChange(decimal? changeKg, GoalDirection direction)
{
    if (!changeKg.HasValue || direction == GoalDirection.None)
    {
        return DirectionalStatus.Unknown;
    }

    if (Math.Abs(changeKg.Value) <= MaintenanceToleranceKg)
    {
        return DirectionalStatus.Neutral;
    }

    return direction switch
    {
        GoalDirection.Loss => changeKg.Value < 0 ? DirectionalStatus.TowardGoal : DirectionalStatus.AwayFromGoal,
        GoalDirection.Gain => changeKg.Value > 0 ? DirectionalStatus.TowardGoal : DirectionalStatus.AwayFromGoal,
        GoalDirection.Maintenance => DirectionalStatus.AwayFromGoal,
        _ => DirectionalStatus.Unknown
    };
}
```

- [ ] **Step 4: Add forecast helpers**

Inside `MetricsService`, add these private helpers before `StartOfWeek`:

```csharp
private static GoalForecast BuildGoalForecast(
    IReadOnlyList<WeightEntry> entries,
    decimal? goalWeightKg,
    GoalDirection direction)
{
    if (!goalWeightKg.HasValue || direction == GoalDirection.None)
    {
        return new GoalForecast(GoalForecastStatus.NoGoal, null, null, null, null);
    }

    if (entries.Count == 0)
    {
        return new GoalForecast(GoalForecastStatus.NoLatestWeight, null, null, null, null);
    }

    if (direction == GoalDirection.Maintenance)
    {
        return new GoalForecast(GoalForecastStatus.AtGoal, entries[^1].EntryDate, null, null, 0);
    }

    var latest = entries[^1];
    var attempts = new[]
    {
        (WindowDays: 30, Label: "30-day"),
        (WindowDays: 90, Label: "90-day"),
        (WindowDays: (int?)null, Label: "all-time")
    };

    var sawAwayFromGoal = false;
    var sawFlatPace = false;

    foreach (var attempt in attempts)
    {
        var baseline = FindForecastBaseline(entries, latest.EntryDate, attempt.WindowDays);
        if (baseline is null)
        {
            continue;
        }

        var elapsedDays = latest.EntryDate.DayNumber - baseline.EntryDate.DayNumber;
        if (elapsedDays <= 0)
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

        if (status != DirectionalStatus.TowardGoal || Math.Abs(dailyPaceKg) < MinimumProjectionPaceKgPerDay)
        {
            sawFlatPace = true;
            continue;
        }

        var remainingKg = direction == GoalDirection.Loss
            ? latest.WeightKg - goalWeightKg.Value
            : goalWeightKg.Value - latest.WeightKg;
        var daysToGoal = (int)Math.Ceiling(remainingKg / Math.Abs(dailyPaceKg));
        if (daysToGoal < 0 || daysToGoal > MaximumProjectionDays)
        {
            sawFlatPace = true;
            continue;
        }

        return new GoalForecast(
            GoalForecastStatus.Estimated,
            latest.EntryDate.AddDays(daysToGoal),
            attempt.Label,
            decimal.Round(dailyPaceKg, 4),
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

private static WeightEntry? FindForecastBaseline(
    IReadOnlyList<WeightEntry> entries,
    DateOnly latestDate,
    int? windowDays)
{
    if (entries.Count < 2)
    {
        return null;
    }

    if (!windowDays.HasValue)
    {
        return entries[0].EntryDate == latestDate ? null : entries[0];
    }

    var targetDate = latestDate.AddDays(-windowDays.Value);
    return entries.LastOrDefault(entry => entry.EntryDate <= targetDate)
        ?? entries.FirstOrDefault(entry => entry.EntryDate > targetDate && entry.EntryDate < latestDate);
}
```

- [ ] **Step 5: Add record helpers**

Inside `MetricsService`, add these private helpers before `StartOfWeek`:

```csharp
private static IReadOnlyList<GoalProgressRecord> BuildGoalProgressRecords(
    IReadOnlyList<WeightEntry> entries,
    GoalDirection direction)
{
    if (entries.Count < 2 || direction is GoalDirection.None or GoalDirection.Maintenance)
    {
        return [];
    }

    var windows = new int?[] { 7, 30, 90, null };
    return windows
        .Select(windowDays => FindBestRecord(entries, direction, windowDays))
        .Where(record => record is not null)
        .Cast<GoalProgressRecord>()
        .ToList();
}

private static GoalProgressRecord? FindBestRecord(
    IReadOnlyList<WeightEntry> entries,
    GoalDirection direction,
    int? windowDays)
{
    GoalProgressRecord? bestRecord = null;
    decimal bestProgressKg = 0;

    for (var startIndex = 0; startIndex < entries.Count - 1; startIndex++)
    {
        var start = entries[startIndex];
        for (var endIndex = startIndex + 1; endIndex < entries.Count; endIndex++)
        {
            var end = entries[endIndex];
            var days = end.EntryDate.DayNumber - start.EntryDate.DayNumber;
            if (windowDays.HasValue && days > windowDays.Value)
            {
                break;
            }

            var changeKg = end.WeightKg - start.WeightKg;
            var progressKg = direction == GoalDirection.Loss ? -changeKg : changeKg;
            if (progressKg <= bestProgressKg)
            {
                continue;
            }

            bestProgressKg = progressKg;
            bestRecord = new GoalProgressRecord(windowDays, changeKg, start.EntryDate, end.EntryDate);
        }
    }

    return bestRecord;
}
```

- [ ] **Step 6: Run metric tests and verify they pass**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter MetricsServiceTests
```

Expected: PASS for all `MetricsServiceTests`.

- [ ] **Step 7: Commit the metric implementation**

```powershell
git add src\WeightTracker.Web\Services\MetricsService.cs
git commit -m "feat: add motivational dashboard metrics"
```

## Task 3: Expose Formatting Helpers From The Dashboard Page Model

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml.cs`
- Test: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Add the progress insights property**

In `IndexModel`, add this property near `Summary`:

```csharp
public GoalProgressInsights ProgressInsights { get; private set; } = new(
    GoalDirection.None,
    DirectionalStatus.Unknown,
    DirectionalStatus.Unknown,
    DirectionalStatus.Unknown,
    new GoalForecast(GoalForecastStatus.NoGoal, null, null, null, null),
    []);
```

- [ ] **Step 2: Populate progress insights during load**

In `LoadAsync`, immediately after `Summary = metricsService.BuildSummary(...)`, add:

```csharp
ProgressInsights = metricsService.BuildMotivationalInsights(entries, Today, settings.WeekStartsOn, settings.GoalWeightKg);
```

- [ ] **Step 3: Add formatting helpers**

In `IndexModel`, add these public helpers near the existing formatting helpers:

```csharp
public string DirectionStatusClass(DirectionalStatus status)
{
    return status switch
    {
        DirectionalStatus.TowardGoal => "metric-status--toward",
        DirectionalStatus.AwayFromGoal => "metric-status--away",
        DirectionalStatus.Neutral => "metric-status--neutral",
        _ => "metric-status--unknown"
    };
}

public string DirectionArrow(decimal? changeKg, DirectionalStatus status)
{
    if (status == DirectionalStatus.Unknown || !changeKg.HasValue)
    {
        return string.Empty;
    }

    if (Math.Abs(changeKg.Value) < 0.05m)
    {
        return "→";
    }

    return changeKg.Value < 0 ? "↓" : "↑";
}

public string FormatForecastValue()
{
    return ProgressInsights.Forecast.Status switch
    {
        GoalForecastStatus.Estimated when ProgressInsights.Forecast.EstimatedDate.HasValue
            => $"Estimated {ProgressInsights.Forecast.EstimatedDate.Value:MMM yyyy}",
        GoalForecastStatus.AtGoal => "At goal",
        GoalForecastStatus.MovingAwayFromGoal => "Moving away from goal",
        GoalForecastStatus.PaceTooFlat => "Pace too flat to project",
        GoalForecastStatus.NeedMoreData => "Need more recent data",
        GoalForecastStatus.NoLatestWeight => "Waiting for first weight",
        _ => "Set a goal to unlock forecast"
    };
}

public string FormatForecastDetail()
{
    return ProgressInsights.Forecast.Status switch
    {
        GoalForecastStatus.Estimated when ProgressInsights.Forecast.SourceWindow is not null
            => $"Based on {ProgressInsights.Forecast.SourceWindow} pace",
        GoalForecastStatus.AtGoal => "Maintenance target reached",
        GoalForecastStatus.MovingAwayFromGoal => "Recent pace is not closing the gap",
        GoalForecastStatus.PaceTooFlat => "Recent movement is too small",
        GoalForecastStatus.NeedMoreData => "Add more entries to estimate pace",
        GoalForecastStatus.NoLatestWeight => "Add your first entry",
        _ => "Goal-aware estimates need a target"
    };
}

public string FormatRecordLabel(GoalProgressRecord record)
{
    return record.WindowDays.HasValue
        ? $"Best {record.WindowDays.Value}-day progress"
        : "Best all-time progress";
}

public string FormatRecordRange(GoalProgressRecord record)
{
    return $"{record.StartDate:dd MMM} to {record.EndDate:dd MMM}";
}
```

- [ ] **Step 4: Run the dashboard test project and verify compile errors are gone**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter DashboardPageTests
```

Expected: existing page tests may fail on text changes after the next task, but the project should compile.

- [ ] **Step 5: Commit the page model changes**

```powershell
git add src\WeightTracker.Web\Pages\Index.cshtml.cs
git commit -m "feat: expose motivational insights to dashboard"
```

## Task 4: Render Progress Insights And Directional UI

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml`
- Modify: `src/WeightTracker.Web/wwwroot/css/site.css`
- Test: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Hide the validation summary when valid**

In `Index.cshtml`, replace:

```cshtml
<div asp-validation-summary="All" class="validation-summary"></div>
```

with:

```cshtml
@if (!ViewData.ModelState.IsValid)
{
    <div asp-validation-summary="All" class="validation-summary"></div>
}
```

- [ ] **Step 2: Replace the insight panel markup**

In `Index.cshtml`, replace the existing `<section class="insights-panel" aria-label="Weight insights">...</section>` with:

```cshtml
<section class="insights-panel" aria-label="Progress insights">
    <div class="section-heading">
        <h2>Progress insights</h2>
        <span>Goal-aware view</span>
    </div>
    <div class="forecast-card">
        <span>Goal forecast</span>
        <strong>@Model.FormatForecastValue()</strong>
        <small>@Model.FormatForecastDetail()</small>
    </div>
    <div class="insight-grid">
        <article>
            <span>Latest</span>
            <strong>@Model.FormatWeight(Model.Summary.LatestWeightKg)</strong>
        </article>
        <article>
            <span>High</span>
            <strong>@Model.FormatWeight(Model.Summary.RangeHighKg)</strong>
        </article>
        <article>
            <span>Low</span>
            <strong>@Model.FormatWeight(Model.Summary.RangeLowKg)</strong>
        </article>
        <article>
            <span>30-day</span>
            <strong class="insight-value @Model.DirectionStatusClass(Model.ProgressInsights.ThirtyDayStatus)">
                @Model.FormatSignedWeight(Model.Summary.ThirtyDayChangeKg)
                <span aria-hidden="true">@Model.DirectionArrow(Model.Summary.ThirtyDayChangeKg, Model.ProgressInsights.ThirtyDayStatus)</span>
            </strong>
        </article>
        <article>
            <span>90-day</span>
            <strong class="insight-value @Model.DirectionStatusClass(Model.ProgressInsights.NinetyDayStatus)">
                @Model.FormatSignedWeight(Model.Summary.NinetyDayChangeKg)
                <span aria-hidden="true">@Model.DirectionArrow(Model.Summary.NinetyDayChangeKg, Model.ProgressInsights.NinetyDayStatus)</span>
            </strong>
        </article>
        <article>
            <span>Entry count</span>
            <strong>@Model.EntryCount</strong>
        </article>
    </div>
    @if (Model.ProgressInsights.Records.Count > 0)
    {
        <div class="record-grid" aria-label="Personal records">
            @foreach (var record in Model.ProgressInsights.Records)
            {
                <article>
                    <span>@Model.FormatRecordLabel(record)</span>
                    <strong>@Model.FormatSignedWeight(record.ChangeKg)</strong>
                    <small>@Model.FormatRecordRange(record)</small>
                </article>
            }
        </div>
    }
    else
    {
        <p class="insight-note">Set a goal to unlock goal-direction records.</p>
    }
</section>
```

- [ ] **Step 3: Add CSS for compact direction and record UI**

In `site.css`, add these rules after the existing `.insight-grid strong` block:

```css
.insight-value {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.insight-value span {
  font-size: 0.9rem;
  line-height: 1;
}

.metric-status--toward {
  color: #2bd46f;
}

.metric-status--away {
  color: #ff3347;
}

.metric-status--neutral,
.metric-status--unknown {
  color: var(--text);
}

.forecast-card,
.record-grid article {
  min-width: 0;
  padding: 12px;
  border: 1px solid var(--line);
  border-radius: 8px;
  background: #0d121b;
}

.forecast-card {
  display: grid;
  gap: 5px;
  margin-top: 12px;
}

.forecast-card span,
.record-grid span,
.forecast-card small,
.record-grid small,
.insight-note {
  color: var(--muted);
}

.forecast-card span,
.record-grid span {
  font-size: 0.72rem;
}

.forecast-card strong,
.record-grid strong {
  font-size: 1rem;
  line-height: 1.15;
}

.forecast-card small,
.record-grid small {
  font-size: 0.72rem;
}

.record-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
  margin-top: 12px;
}

.record-grid article {
  display: grid;
  gap: 5px;
}

.record-grid span,
.record-grid strong,
.record-grid small {
  display: block;
  overflow-wrap: anywhere;
}

.insight-note {
  margin: 12px 0 0;
  font-size: 0.86rem;
}
```

In the existing `@media (min-width: 768px)` block, add:

```css
.record-grid {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}
```

- [ ] **Step 4: Run dashboard page tests and collect expected text failures**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter DashboardPageTests
```

Expected: FAIL where tests still assert `Insights` instead of `Progress insights`, or where new rendered sections are not asserted yet.

- [ ] **Step 5: Commit the UI rendering changes**

```powershell
git add src\WeightTracker.Web\Pages\Index.cshtml src\WeightTracker.Web\wwwroot\css\site.css
git commit -m "feat: render progress insights"
```

## Task 5: Update Dashboard Page Tests

**Files:**
- Modify: `tests/WeightTracker.Tests/DashboardPageTests.cs`
- Test: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Update deep insight assertions**

In `Dashboard_RendersDeepInsightSectionsWithAllTimeData`, replace:

```csharp
Assert.Contains("Insights", html);
```

with:

```csharp
Assert.Contains("Progress insights", html);
Assert.Contains("Goal forecast", html);
Assert.Contains("Set a goal to unlock forecast", html);
Assert.Contains("Set a goal to unlock goal-direction records.", html);
```

In `Dashboard_WithNoEntries_RendersEmptyDeepInsights`, replace:

```csharp
Assert.Contains("Insights", html);
```

with:

```csharp
Assert.Contains("Progress insights", html);
Assert.Contains("Goal forecast", html);
Assert.Contains("Set a goal to unlock forecast", html);
```

- [ ] **Step 2: Add no-empty-validation-strip assertion**

In `Dashboard_RendersMobileDashboardWithCalendarEntryDialog`, add:

```csharp
Assert.DoesNotContain("validation-summary", html);
```

- [ ] **Step 3: Add goal forecast and records page test**

Add this test before `Dashboard_WithNoGoal_RendersGoalPanelAndSetAction`:

```csharp
[Fact]
public async Task Dashboard_WithGoal_RendersForecastAndGoalDirectionRecords()
{
    await using var app = new DashboardTestApp();
    await app.UpdateSettingsAsync("kg", goalWeightKg: 80m);
    await app.AddEntryAsync(new DateOnly(2026, 5, 27), 86.0m);
    await app.AddEntryAsync(new DateOnly(2026, 6, 18), 84.0m);
    await app.AddEntryAsync(Today, 83.0m);
    var client = app.CreateClient();

    var response = await client.GetAsync("/");
    var html = await response.Content.ReadAsStringAsync();
    var decoded = WebUtility.HtmlDecode(html);

    Assert.True(response.StatusCode == HttpStatusCode.OK, html);
    Assert.Contains("Progress insights", html);
    Assert.Contains("Goal forecast", html);
    Assert.Contains("Estimated Jul 2026", html);
    Assert.Contains("Based on 30-day pace", html);
    Assert.Contains("Best 7-day progress", html);
    Assert.Contains("Best 30-day progress", html);
    Assert.Contains("-1.0 kg", decoded);
    Assert.Contains("-3.0 kg", decoded);
    Assert.Contains("18 Jun to 26 Jun", html);
    Assert.Contains("27 May to 26 Jun", html);
    Assert.Contains("metric-status--toward", html);
}
```

- [ ] **Step 4: Add validation error assertion**

In `Save_WithInvalidWeight_ReturnsValidationAndDoesNotPersist`, after the existing validation assertion, add:

```csharp
Assert.Contains("validation-summary", html);
```

- [ ] **Step 5: Run dashboard page tests and verify they pass**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter DashboardPageTests
```

Expected: PASS for all `DashboardPageTests`.

- [ ] **Step 6: Commit the updated page tests**

```powershell
git add tests\WeightTracker.Tests\DashboardPageTests.cs
git commit -m "test: cover progress insight rendering"
```

## Task 6: Full Verification And Roadmap Note

**Files:**
- Modify: `docs/ROADMAP.md`
- Test: `WeightTracker.sln`

- [ ] **Step 1: Update roadmap completed and later work notes**

In `docs/ROADMAP.md`, under `Completed Foundation`, add:

```markdown
- Goal-aware motivational dashboard insights with guarded forecast and goal-direction records.
```

Under `Later Work`, add:

```markdown
- Add weekly delta bar charts using configured calendar weeks.
```

- [ ] **Step 2: Run full test suite**

Run:

```powershell
dotnet test WeightTracker.sln
```

Expected: PASS for all tests.

- [ ] **Step 3: Check for line-ending and whitespace issues**

Run:

```powershell
git diff --check
```

Expected: no output and exit code `0`.

- [ ] **Step 4: Inspect final status**

Run:

```powershell
git status --short --branch
```

Expected: current branch is the feature branch and only the intended files are modified before the final commit.

- [ ] **Step 5: Commit roadmap and final verification changes**

```powershell
git add docs\ROADMAP.md
git commit -m "docs: update roadmap for motivational insights"
```

## Self-Review Checklist

- Spec coverage:
  - Empty red validation strip: Task 4 and Task 5.
  - Goal-aware direction indicators: Task 2, Task 3, Task 4, Task 5.
  - Guarded goal forecast: Task 1, Task 2, Task 3, Task 4, Task 5.
  - Goal-direction records: Task 1, Task 2, Task 3, Task 4, Task 5.
  - Weekly delta chart kept separate: Task 6 roadmap note.
- Type consistency:
  - `GoalProgressInsights`, `GoalForecast`, `GoalProgressRecord`, `GoalDirection`, `DirectionalStatus`, and `GoalForecastStatus` are introduced in Task 2 before use in page code.
  - `ProgressInsights` is introduced in Task 3 before Razor references in Task 4.
  - Formatting helper names match the Razor markup.
- Verification:
  - Metrics tests run after service work.
  - Dashboard tests run after rendering work.
  - Full solution tests run before final handoff.
