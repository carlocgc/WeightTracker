# Weekly Delta Bar Graph Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a dashboard `Weekly change` bar chart that shows the last 12 configured week-over-week average weight deltas.

**Architecture:** Compute weekly delta buckets server-side in `MetricsService` so week boundaries, sparse data, and goal-aware status are testable. Expose the series from `IndexModel`, serialize it in `Index.cshtml`, and render it with the existing Chart.js dependency. Keep styling in the current dashboard CSS and avoid new routes, persistence changes, or dependencies.

**Tech Stack:** ASP.NET Core Razor Pages, C#, xUnit, Chart.js, CSS.

---

## File Structure

- Modify `src/WeightTracker.Web/Services/MetricsService.cs`: add the `WeeklyDeltaPoint` record and `BuildWeeklyDeltaSeries` method.
- Modify `tests/WeightTracker.Tests/MetricsServiceTests.cs`: add service-level TDD coverage for weekly bucket construction, averages, sparse weeks, current week, out-of-range baseline, and goal-aware status.
- Modify `src/WeightTracker.Web/Pages/Index.cshtml.cs`: expose `WeeklyDeltas` and load it with the existing dashboard entries.
- Modify `tests/WeightTracker.Tests/DashboardPageTests.cs`: assert that the dashboard renders the new panel, canvas, and serialized data.
- Modify `src/WeightTracker.Web/Pages/Index.cshtml`: add the `Weekly change` panel and Chart.js bar chart initialization.
- Modify `src/WeightTracker.Web/wwwroot/css/site.css`: style the panel and chart frame within the existing dashboard layout.

---

### Task 1: Metrics Service Weekly Delta Model

**Files:**
- Modify: `tests/WeightTracker.Tests/MetricsServiceTests.cs`
- Modify: `src/WeightTracker.Web/Services/MetricsService.cs`

- [ ] **Step 1: Add failing tests for weekly bucket rules**

Add these tests inside `MetricsServiceTests`, above the `Entry` helper:

```csharp
[Fact]
public void BuildWeeklyDeltaSeries_BuildsTwelveConfiguredWeeksEndingAtCurrentWeek()
{
    var entries = new[]
    {
        Entry("2026-06-08", 84.0m),
        Entry("2026-06-15", 83.0m),
        Entry("2026-06-16", 82.0m),
        Entry("2026-06-22", 81.0m),
        Entry("2026-06-26", 80.0m)
    };

    var series = _service.BuildWeeklyDeltaSeries(entries, new DateOnly(2026, 6, 26), DayOfWeek.Monday, null);

    Assert.Equal(12, series.Count);
    Assert.Equal(new DateOnly(2026, 4, 6), series[0].WeekStart);
    Assert.Equal(new DateOnly(2026, 4, 12), series[0].WeekEnd);
    Assert.Equal(new DateOnly(2026, 6, 22), series[^1].WeekStart);
    Assert.Equal(new DateOnly(2026, 6, 26), series[^1].WeekEnd);
    Assert.True(series[^1].IsCurrentWeek);
}

[Fact]
public void BuildWeeklyDeltaSeries_UsesWeeklyAveragesRatherThanLastEntries()
{
    var entries = new[]
    {
        Entry("2026-06-09", 90.0m),
        Entry("2026-06-10", 80.0m),
        Entry("2026-06-16", 84.0m),
        Entry("2026-06-17", 82.0m)
    };

    var series = _service.BuildWeeklyDeltaSeries(entries, new DateOnly(2026, 6, 20), DayOfWeek.Monday, null);
    var week = Assert.Single(series, point => point.WeekStart == new DateOnly(2026, 6, 15));

    Assert.Equal(83.0m, week.CurrentWeekAverageKg);
    Assert.Equal(85.0m, week.PreviousWeekAverageKg);
    Assert.Equal(-2.0m, week.DeltaKg);
}

[Fact]
public void BuildWeeklyDeltaSeries_PreservesBucketsButReturnsNullDeltaForMissingAdjacentWeek()
{
    var entries = new[]
    {
        Entry("2026-06-03", 86.0m),
        Entry("2026-06-17", 84.0m)
    };

    var series = _service.BuildWeeklyDeltaSeries(entries, new DateOnly(2026, 6, 20), DayOfWeek.Monday, null);
    var week = Assert.Single(series, point => point.WeekStart == new DateOnly(2026, 6, 15));

    Assert.Equal(84.0m, week.CurrentWeekAverageKg);
    Assert.Null(week.PreviousWeekAverageKg);
    Assert.Null(week.DeltaKg);
    Assert.Equal(DirectionalStatus.Unknown, week.DirectionalStatus);
}

[Fact]
public void BuildWeeklyDeltaSeries_UsesPreviousOutOfRangeWeekForFirstVisibleDelta()
{
    var entries = new[]
    {
        Entry("2026-03-31", 90.0m),
        Entry("2026-04-07", 88.0m)
    };

    var series = _service.BuildWeeklyDeltaSeries(entries, new DateOnly(2026, 6, 26), DayOfWeek.Monday, 80.0m);
    var firstVisible = series[0];

    Assert.Equal(new DateOnly(2026, 4, 6), firstVisible.WeekStart);
    Assert.Equal(88.0m, firstVisible.CurrentWeekAverageKg);
    Assert.Equal(90.0m, firstVisible.PreviousWeekAverageKg);
    Assert.Equal(-2.0m, firstVisible.DeltaKg);
    Assert.Equal(DirectionalStatus.TowardGoal, firstVisible.DirectionalStatus);
}

[Fact]
public void BuildWeeklyDeltaSeries_ClassifiesGainGoalAndMaintenanceGoalDeltas()
{
    var entries = new[]
    {
        Entry("2026-06-09", 80.0m),
        Entry("2026-06-16", 82.0m),
        Entry("2026-06-23", 82.03m)
    };

    var gainSeries = _service.BuildWeeklyDeltaSeries(entries, new DateOnly(2026, 6, 26), DayOfWeek.Monday, 86.0m);
    var gainWeek = Assert.Single(gainSeries, point => point.WeekStart == new DateOnly(2026, 6, 15));
    Assert.Equal(DirectionalStatus.TowardGoal, gainWeek.DirectionalStatus);

    var maintenanceSeries = _service.BuildWeeklyDeltaSeries(entries, new DateOnly(2026, 6, 26), DayOfWeek.Monday, 82.0m);
    var maintenanceWeek = Assert.Single(maintenanceSeries, point => point.WeekStart == new DateOnly(2026, 6, 22));
    Assert.Equal(DirectionalStatus.Neutral, maintenanceWeek.DirectionalStatus);
}
```

- [ ] **Step 2: Run metrics tests and verify they fail**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter FullyQualifiedName~MetricsServiceTests
```

Expected: build fails because `MetricsService.BuildWeeklyDeltaSeries` and `WeeklyDeltaPoint` do not exist.

- [ ] **Step 3: Add the weekly delta record and service method**

In `src/WeightTracker.Web/Services/MetricsService.cs`, add this record after `ChartSeries`:

```csharp
public sealed record WeeklyDeltaPoint(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    decimal? CurrentWeekAverageKg,
    decimal? PreviousWeekAverageKg,
    decimal? DeltaKg,
    bool IsCurrentWeek,
    DirectionalStatus DirectionalStatus);
```

Add this public method after `BuildChartSeries`:

```csharp
public IReadOnlyList<WeeklyDeltaPoint> BuildWeeklyDeltaSeries(
    IEnumerable<WeightEntry> source,
    DateOnly today,
    DayOfWeek weekStartsOn,
    decimal? goalWeightKg)
{
    var entries = source
        .Where(item => item.EntryDate <= today)
        .OrderBy(item => item.EntryDate)
        .ToList();
    var currentWeekStart = StartOfWeek(today, weekStartsOn);
    var firstVisibleWeekStart = currentWeekStart.AddDays(-77);
    var latestWeightKg = entries.Count == 0 ? null : entries[^1].WeightKg;
    var direction = DetermineGoalDirection(latestWeightKg, goalWeightKg);
    var points = new List<WeeklyDeltaPoint>();

    for (var offset = 0; offset < 12; offset++)
    {
        var weekStart = firstVisibleWeekStart.AddDays(offset * 7);
        var configuredWeekEnd = weekStart.AddDays(6);
        var weekEnd = configuredWeekEnd > today ? today : configuredWeekEnd;
        var previousWeekStart = weekStart.AddDays(-7);
        var previousWeekEnd = weekStart.AddDays(-1);
        var currentAverage = AverageForRange(entries, weekStart, weekEnd);
        var previousAverage = AverageForRange(entries, previousWeekStart, previousWeekEnd);
        var delta = currentAverage.HasValue && previousAverage.HasValue
            ? currentAverage.Value - previousAverage.Value
            : null;

        points.Add(new WeeklyDeltaPoint(
            weekStart,
            weekEnd,
            currentAverage,
            previousAverage,
            delta,
            weekStart == currentWeekStart,
            ClassifyChange(delta, direction)));
    }

    return points;
}
```

- [ ] **Step 4: Run metrics tests and verify they pass**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter FullyQualifiedName~MetricsServiceTests
```

Expected: all `MetricsServiceTests` pass.

- [ ] **Step 5: Commit metrics service changes**

Run:

```powershell
git add src\WeightTracker.Web\Services\MetricsService.cs tests\WeightTracker.Tests\MetricsServiceTests.cs
git commit -m "Add weekly delta metrics"
```

---

### Task 2: Page Model And Dashboard Rendering Tests

**Files:**
- Modify: `tests/WeightTracker.Tests/DashboardPageTests.cs`
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml.cs`

- [ ] **Step 1: Add a failing dashboard page test**

Add this test in `DashboardPageTests` after `Dashboard_RendersDeepInsightSectionsWithAllTimeData`:

```csharp
[Fact]
public async Task Dashboard_RendersWeeklyChangePanelWithSerializedDeltaData()
{
    await using var app = new DashboardTestApp();
    await app.UpdateSettingsAsync("kg", goalWeightKg: 80m);
    await app.AddEntryAsync(new DateOnly(2026, 6, 10), 86.0m);
    await app.AddEntryAsync(new DateOnly(2026, 6, 17), 84.0m);
    await app.AddEntryAsync(Today, 83.0m);
    var client = app.CreateClient();

    var response = await client.GetAsync("/");
    var html = await response.Content.ReadAsStringAsync();

    Assert.True(response.StatusCode == HttpStatusCode.OK, html);
    Assert.Contains("aria-label=\"Weekly change\"", html);
    Assert.Contains("id=\"weeklyDeltaChart\"", html);
    Assert.Contains("const weeklyDeltas =", html);
    Assert.Contains("\"weekStart\":\"2026-06-15\"", html);
    Assert.Contains("\"weekEnd\":\"2026-06-21\"", html);
    Assert.Contains("\"deltaKg\":-2.000", html);
    Assert.Contains("\"directionalStatus\":2", html);
    Assert.Contains("Progress insights", html);
    Assert.Contains("Recent history", html);
}
```

- [ ] **Step 2: Run dashboard test and verify it fails**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter FullyQualifiedName~DashboardPageTests.Dashboard_RendersWeeklyChangePanelWithSerializedDeltaData
```

Expected: fail because the page does not expose or render the weekly chart.

- [ ] **Step 3: Expose weekly deltas from `IndexModel`**

In `src/WeightTracker.Web/Pages/Index.cshtml.cs`, add this property after `LongRangeChart`:

```csharp
public IReadOnlyList<WeeklyDeltaPoint> WeeklyDeltas { get; private set; } = [];
```

In `LoadAsync`, after `LongRangeChart = metricsService.BuildChartSeries(entries, settings.WeekStartsOn, settings.GoalWeightKg);`, add:

```csharp
WeeklyDeltas = metricsService.BuildWeeklyDeltaSeries(entries, Today, settings.WeekStartsOn, settings.GoalWeightKg);
```

- [ ] **Step 4: Run dashboard test and confirm it still fails at rendering**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter FullyQualifiedName~DashboardPageTests.Dashboard_RendersWeeklyChangePanelWithSerializedDeltaData
```

Expected: fail because `Index.cshtml` has not rendered the panel and serialized data yet.

---

### Task 3: Razor Markup And Chart.js Rendering

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml`
- Modify: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Add the weekly panel markup**

In `src/WeightTracker.Web/Pages/Index.cshtml`, add this section inside `<div class="dashboard-supporting">`, before the existing `Recent history` section:

```cshtml
<section class="weekly-change-panel" aria-label="Weekly change">
    <div class="section-heading">
        <h2>Weekly change</h2>
        <span>Last 12 weeks</span>
    </div>
    <div class="weekly-delta-chart-frame">
        <canvas id="weeklyDeltaChart"></canvas>
    </div>
</section>
```

- [ ] **Step 2: Add the weekly chart script helpers**

In the existing `<script>` block in `Index.cshtml`, add these functions after `createTrendChart`:

```javascript
function signedWeightLabel(valueKg) {
    if (valueKg === null || valueKg === undefined) {
        return '-';
    }

    const converted = valueKg * unitMultiplier;
    const rounded = Math.round(converted * 100) / 100;
    return `${rounded > 0 ? '+' : ''}${rounded.toFixed(Math.abs(rounded) >= 10 ? 1 : 2)} ${displayUnit}`;
}

function weightLabel(valueKg) {
    if (valueKg === null || valueKg === undefined) {
        return '-';
    }

    const converted = valueKg * unitMultiplier;
    const rounded = Math.round(converted * 100) / 100;
    return `${rounded.toFixed(Math.abs(rounded) >= 10 ? 1 : 2)} ${displayUnit}`;
}

function weeklyDeltaColor(point) {
    if (point.deltaKg === null || point.deltaKg === undefined) {
        return 'rgba(125, 135, 152, 0.22)';
    }

    if (point.directionalStatus === 2) {
        return 'rgba(133, 232, 157, 0.82)';
    }

    if (point.directionalStatus === 3) {
        return 'rgba(255, 111, 143, 0.82)';
    }

    if (Math.abs(point.deltaKg) <= 0.05) {
        return 'rgba(248, 193, 74, 0.72)';
    }

    return point.deltaKg < 0
        ? 'rgba(40, 240, 212, 0.76)'
        : 'rgba(248, 193, 74, 0.76)';
}

function createWeeklyDeltaChart(canvasId, points) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || !window.Chart) {
        return null;
    }

    return new Chart(canvas, {
        type: 'bar',
        data: {
            labels: points.map((point) => point.isCurrentWeek ? 'This week' : point.weekStart.slice(5)),
            datasets: [
                {
                    label: 'Weekly change',
                    data: points.map((point) => point.deltaKg),
                    backgroundColor: points.map(weeklyDeltaColor),
                    borderWidth: 0,
                    borderRadius: 4
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        title(items) {
                            const point = points[items[0].dataIndex];
                            return point.isCurrentWeek ? 'This week' : `${point.weekStart} to ${point.weekEnd}`;
                        },
                        label(item) {
                            const point = points[item.dataIndex];
                            return `Delta: ${signedWeightLabel(point.deltaKg)}`;
                        },
                        afterLabel(item) {
                            const point = points[item.dataIndex];
                            return [
                                `Average: ${weightLabel(point.currentWeekAverageKg)}`,
                                `Previous: ${weightLabel(point.previousWeekAverageKg)}`
                            ];
                        }
                    }
                }
            },
            scales: {
                x: { grid: { display: false }, ticks: { color: '#7d8798', maxTicksLimit: 6 } },
                y: { grid: { color: 'rgba(255,255,255,0.06)' }, ticks: { color: '#7d8798', callback: (value) => signedWeightLabel(value) } }
            }
        }
    });
}
```

Near the existing trend constants, before `const trendDaily = ...`, add:

```javascript
const displayUnit = '@Model.DisplayUnit';
const unitMultiplier = displayUnit === 'lb' ? 2.20462262185 : 1;
```

After the existing `const trendChart = createTrendChart('trendChart', trendDaily, trendMoving, trendGoal);`, add:

```javascript
const weeklyDeltas = @Json.Serialize(Model.WeeklyDeltas);
createWeeklyDeltaChart('weeklyDeltaChart', weeklyDeltas);
```

- [ ] **Step 3: Run dashboard test and verify it passes**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter FullyQualifiedName~DashboardPageTests.Dashboard_RendersWeeklyChangePanelWithSerializedDeltaData
```

Expected: the new dashboard test passes.

- [ ] **Step 4: Commit page model and Razor rendering**

Run:

```powershell
git add src\WeightTracker.Web\Pages\Index.cshtml.cs src\WeightTracker.Web\Pages\Index.cshtml tests\WeightTracker.Tests\DashboardPageTests.cs
git commit -m "Render weekly delta chart"
```

---

### Task 4: Dashboard CSS Integration

**Files:**
- Modify: `src/WeightTracker.Web/wwwroot/css/site.css`
- Test: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Include weekly panel in existing panel styling**

In `site.css`, update the panel selector:

```css
.goal-panel,
.trend-panel,
.weekly-change-panel,
.history-panel,
.insights-panel,
.data-panel,
.metric-strip article,
.insight-grid article {
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--surface-raised);
}
```

Update the padding selector:

```css
.trend-panel,
.weekly-change-panel,
.history-panel,
.insights-panel,
.data-panel {
  padding: 14px;
}
```

Add this block after `.trend-chart-frame`:

```css
.weekly-delta-chart-frame {
  position: relative;
  height: 200px;
  margin-top: 10px;
  overflow: hidden;
}
```

Update the canvas selector:

```css
.trend-panel canvas,
.weekly-change-panel canvas {
  display: block;
  width: 100% !important;
  height: 100% !important;
  min-height: 0;
}
```

- [ ] **Step 2: Update desktop supporting grid for four panels**

In the `@media (min-width: 1024px)` block, replace the `.dashboard-supporting` grid rule with:

```css
.dashboard-supporting {
  grid-column: 1 / -1;
  grid-template-columns: minmax(260px, 0.85fr) minmax(260px, 0.85fr) minmax(0, 1.35fr) minmax(220px, 0.65fr);
  align-items: start;
}
```

Add this rule in the same media block after `.trend-chart-frame`:

```css
.weekly-delta-chart-frame {
  height: 240px;
}
```

- [ ] **Step 3: Run dashboard tests**

Run:

```powershell
dotnet test tests\WeightTracker.Tests\WeightTracker.Tests.csproj --filter FullyQualifiedName~DashboardPageTests
```

Expected: all `DashboardPageTests` pass.

- [ ] **Step 4: Commit CSS changes**

Run:

```powershell
git add src\WeightTracker.Web\wwwroot\css\site.css
git commit -m "Style weekly delta chart"
```

---

### Task 5: Full Verification

**Files:**
- Verify: `WeightTracker.sln`

- [ ] **Step 1: Run full test suite**

Run:

```powershell
dotnet test WeightTracker.sln
```

Expected: all tests pass.

- [ ] **Step 2: Check formatting and working tree**

Run:

```powershell
git diff --check
git status --short --branch
```

Expected: `git diff --check` prints no errors. `git status --short --branch` shows the feature branch with no unstaged or uncommitted changes after the commits.

- [ ] **Step 3: Review final commits**

Run:

```powershell
git log --oneline -5
```

Expected: the latest commits include:

```text
Style weekly delta chart
Render weekly delta chart
Add weekly delta metrics
Add weekly delta bar graph design
```

