# Dashboard Deep Insights Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add deeper scrollable insights to the existing dashboard without adding new pages, full history tables, modals, sticky UI, AJAX, or dependencies.

**Architecture:** Keep `/` as the single Razor-rendered dashboard. Extend `IndexModel` with all-entry insight data, render two read-only sections below the current top dashboard, and reuse the existing Chart.js pattern for a separate all-time chart.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core-backed services, xUnit integration tests, Chart.js already loaded by the dashboard, existing CSS.

---

## File Structure

- Modify `tests/WeightTracker.Tests/DashboardPageTests.cs`: add integration coverage for the deeper dashboard sections, all-time chart data, focused metrics, and no full-history table.
- Modify `src/WeightTracker.Web/Pages/Index.cshtml.cs`: load all entries up to today, keep compact chart scoped to the existing dashboard range, expose `LongRangeChart` and `EntryCount`.
- Modify `src/WeightTracker.Web/Pages/Index.cshtml`: add the long-term trend and insights sections below recent history, and render a second chart from `LongRangeChart`.
- Modify `src/WeightTracker.Web/wwwroot/css/site.css`: style the new sections using the existing compact dark dashboard language.

---

### Task 1: Add Failing Dashboard Tests

**Files:**
- Modify: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Add a test for deep insight sections and all-time data**

Add this test after `Dashboard_CalendarMonthQueryRendersHistoricalEntry`:

```csharp
[Fact]
public async Task Dashboard_RendersDeepInsightSectionsWithAllTimeData()
{
    await using var app = new DashboardTestApp();
    await app.UpdateSettingsAsync("kg");
    await app.AddEntryAsync(new DateOnly(2025, 11, 1), 84.0m);
    await app.AddEntryAsync(new DateOnly(2026, 5, 20), 83.0m);
    await app.AddEntryAsync(Yesterday, 82.4m);
    await app.AddEntryAsync(Today, 82.1m);
    var client = app.CreateClient();

    var response = await client.GetAsync("/");
    var html = await response.Content.ReadAsStringAsync();

    Assert.True(response.StatusCode == HttpStatusCode.OK, html);
    Assert.Contains("Long-term trend", html);
    Assert.Contains("Insights", html);
    Assert.Contains("id=\"longRangeTrendChart\"", html);
    Assert.Contains("\"date\":\"2025-11-01\"", html);
    Assert.Contains("Latest", html);
    Assert.Contains("82.1 kg", html);
    Assert.Contains("High", html);
    Assert.Contains("84.0 kg", html);
    Assert.Contains("Low", html);
    Assert.Contains("-0.9 kg", html);
    Assert.Contains("-1.9 kg", html);
    Assert.Contains("Entry count", html);
    Assert.Contains(">4</strong>", html);
    Assert.DoesNotContain("full-history-list", html);
    Assert.DoesNotContain("All entries", html);
}
```

- [ ] **Step 2: Add a test for empty insight rendering**

Add this test after the deep insight test:

```csharp
[Fact]
public async Task Dashboard_WithNoEntries_RendersEmptyDeepInsights()
{
    await using var app = new DashboardTestApp();
    await app.UpdateSettingsAsync("kg");
    var client = app.CreateClient();

    var response = await client.GetAsync("/");
    var html = await response.Content.ReadAsStringAsync();

    Assert.True(response.StatusCode == HttpStatusCode.OK, html);
    Assert.Contains("Long-term trend", html);
    Assert.Contains("Insights", html);
    Assert.Contains("id=\"longRangeTrendChart\"", html);
    Assert.Contains("Entry count", html);
    Assert.Contains(">0</strong>", html);
    Assert.Contains("No weights recorded yet.", html);
}
```

- [ ] **Step 3: Run the new tests and verify they fail**

Run:

```powershell
dotnet test WeightTracker.sln --filter "Dashboard_RendersDeepInsightSectionsWithAllTimeData|Dashboard_WithNoEntries_RendersEmptyDeepInsights"
```

Expected: both tests fail because the dashboard does not yet render `Long-term trend`, `Insights`, `longRangeTrendChart`, or `Entry count`.

- [ ] **Step 4: Commit the failing tests**

```powershell
git add tests/WeightTracker.Tests/DashboardPageTests.cs
git commit -m "test: cover dashboard deep insights"
```

---

### Task 2: Load All-Entry Insight Data In IndexModel

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml.cs`

- [ ] **Step 1: Add the new page properties**

In `IndexModel`, after the existing `Chart` property, add:

```csharp
public ChartSeries LongRangeChart { get; private set; } = new([], [], [], null);

public int EntryCount { get; private set; }
```

- [ ] **Step 2: Replace range-based loading with all-entry loading**

In `LoadAsync`, replace:

```csharp
var visibleMonthStart = new DateOnly(VisibleMonth.Year, VisibleMonth.Month, 1);
var chartStart = Today.AddDays(-ChartDayCount);
var rangeStart = visibleMonthStart < chartStart ? visibleMonthStart : chartStart;

var entries = await entryService.GetRangeAsync(rangeStart, Today, cancellationToken);
var entriesByDate = entries.ToDictionary(entry => entry.EntryDate);
```

with:

```csharp
var visibleMonthStart = new DateOnly(VisibleMonth.Year, VisibleMonth.Month, 1);
var chartStart = Today.AddDays(-ChartDayCount);

var entries = await entryService.GetRangeAsync(DateOnly.MinValue, Today, cancellationToken);
var compactChartEntries = entries
    .Where(entry => entry.EntryDate >= chartStart)
    .ToList();
var entriesByDate = entries.ToDictionary(entry => entry.EntryDate);
```

- [ ] **Step 3: Set all-entry summary values**

Near the end of `LoadAsync`, replace:

```csharp
Summary = metricsService.BuildSummary(entries, Today, settings.WeekStartsOn, settings.GoalWeightKg);
Chart = metricsService.BuildChartSeries(entries, settings.WeekStartsOn, settings.GoalWeightKg);
```

with:

```csharp
Summary = metricsService.BuildSummary(entries, Today, settings.WeekStartsOn, settings.GoalWeightKg);
Chart = metricsService.BuildChartSeries(compactChartEntries, settings.WeekStartsOn, settings.GoalWeightKg);
LongRangeChart = metricsService.BuildChartSeries(entries, settings.WeekStartsOn, settings.GoalWeightKg);
EntryCount = entries.Count;
```

- [ ] **Step 4: Run the focused tests and verify they still fail only on markup**

Run:

```powershell
dotnet test WeightTracker.sln --filter "Dashboard_RendersDeepInsightSectionsWithAllTimeData|Dashboard_WithNoEntries_RendersEmptyDeepInsights"
```

Expected: tests still fail because the new sections are not rendered yet. The implementation should compile, proving the new model properties are valid.

- [ ] **Step 5: Commit model data changes**

```powershell
git add src/WeightTracker.Web/Pages/Index.cshtml.cs
git commit -m "feat: load dashboard deep insight data"
```

---

### Task 3: Render Long-Term Trend And Insights Sections

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml`

- [ ] **Step 1: Add the long-term trend section**

After the existing `Recent history` section and before the closing `</div>` for `.weight-app`, add:

```cshtml
    <section class="trend-panel trend-panel--long" aria-label="Long-term trend">
        <div class="section-heading">
            <h2>Long-term trend</h2>
            <span>@Model.EntryCount entries</span>
        </div>
        <div class="trend-chart-frame trend-chart-frame--long">
            <canvas id="longRangeTrendChart"></canvas>
        </div>
    </section>
```

- [ ] **Step 2: Add the insights metrics section**

Immediately after the long-term trend section, add:

```cshtml
    <section class="insights-panel" aria-label="Weight insights">
        <div class="section-heading">
            <h2>Insights</h2>
            <span>All recorded weights</span>
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
                <strong>@Model.FormatSignedWeight(Model.Summary.ThirtyDayChangeKg)</strong>
            </article>
            <article>
                <span>90-day</span>
                <strong>@Model.FormatSignedWeight(Model.Summary.NinetyDayChangeKg)</strong>
            </article>
            <article>
                <span>Entry count</span>
                <strong>@Model.EntryCount</strong>
            </article>
        </div>
    </section>
```

- [ ] **Step 3: Replace the single chart script with a reusable renderer**

Replace the block from:

```javascript
const daily = @Json.Serialize(Model.Chart.DailyWeights);
const moving = @Json.Serialize(Model.Chart.MovingAverages);
const goal = @Json.Serialize(Model.Chart.GoalWeightKg);
const labels = daily.map(point => point.date);
const canvas = document.getElementById('trendChart');
if (canvas && window.Chart) {
    new Chart(canvas, {
        type: 'line',
        data: {
            labels,
            datasets: [
                { label: 'Daily', data: daily.map(point => point.weightKg), borderColor: '#28f0d4', backgroundColor: 'rgba(40, 240, 212, 0.12)', tension: 0.35, pointRadius: 2 },
                { label: '7-day avg', data: moving.map(point => point.weightKg), borderColor: '#f8c14a', tension: 0.35, pointRadius: 0 },
                { label: 'Goal', data: goal ? labels.map(() => goal) : [], borderColor: '#85e89d', borderDash: [6, 6], pointRadius: 0 }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { color: 'rgba(255,255,255,0.04)' }, ticks: { color: '#7d8798', maxTicksLimit: 4 } },
                y: { grid: { color: 'rgba(255,255,255,0.06)' }, ticks: { color: '#7d8798' } }
            }
        }
    });
}
```

with:

```javascript
function renderTrendChart(canvasId, dailyPoints, movingPoints, goalWeight, maxTicksLimit) {
    const labels = dailyPoints.map(point => point.date);
    const canvas = document.getElementById(canvasId);
    if (!canvas || !window.Chart) {
        return;
    }

    new Chart(canvas, {
        type: 'line',
        data: {
            labels,
            datasets: [
                { label: 'Daily', data: dailyPoints.map(point => point.weightKg), borderColor: '#28f0d4', backgroundColor: 'rgba(40, 240, 212, 0.12)', tension: 0.35, pointRadius: 2 },
                { label: '7-day avg', data: movingPoints.map(point => point.weightKg), borderColor: '#f8c14a', tension: 0.35, pointRadius: 0 },
                { label: 'Goal', data: goalWeight ? labels.map(() => goalWeight) : [], borderColor: '#85e89d', borderDash: [6, 6], pointRadius: 0 }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { color: 'rgba(255,255,255,0.04)' }, ticks: { color: '#7d8798', maxTicksLimit } },
                y: { grid: { color: 'rgba(255,255,255,0.06)' }, ticks: { color: '#7d8798' } }
            }
        }
    });
}

const daily = @Json.Serialize(Model.Chart.DailyWeights);
const moving = @Json.Serialize(Model.Chart.MovingAverages);
const goal = @Json.Serialize(Model.Chart.GoalWeightKg);
renderTrendChart('trendChart', daily, moving, goal, 4);

const longRangeDaily = @Json.Serialize(Model.LongRangeChart.DailyWeights);
const longRangeMoving = @Json.Serialize(Model.LongRangeChart.MovingAverages);
const longRangeGoal = @Json.Serialize(Model.LongRangeChart.GoalWeightKg);
renderTrendChart('longRangeTrendChart', longRangeDaily, longRangeMoving, longRangeGoal, 6);
```

- [ ] **Step 4: Run focused tests**

Run:

```powershell
dotnet test WeightTracker.sln --filter "Dashboard_RendersDeepInsightSectionsWithAllTimeData|Dashboard_WithNoEntries_RendersEmptyDeepInsights"
```

Expected: tests pass or fail only on styling-independent string details. If the tests fail because of exact text, inspect the rendered HTML and adjust the assertions or markup to match the approved design.

- [ ] **Step 5: Commit markup and chart rendering**

```powershell
git add src/WeightTracker.Web/Pages/Index.cshtml
git commit -m "feat: render dashboard deep insights"
```

---

### Task 4: Style The New Deep Insight Sections

**Files:**
- Modify: `src/WeightTracker.Web/wwwroot/css/site.css`

- [ ] **Step 1: Include the new panels in the existing panel styling**

Replace:

```css
.weight-hero,
.trend-panel,
.history-panel,
.metric-strip article {
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--surface-raised);
}
```

with:

```css
.weight-hero,
.trend-panel,
.history-panel,
.insights-panel,
.metric-strip article,
.insight-grid article {
  border: 1px solid var(--line);
  border-radius: 8px;
  background: var(--surface-raised);
}
```

- [ ] **Step 2: Include the insights panel in section padding**

Replace:

```css
.trend-panel,
.history-panel {
  padding: 14px;
}
```

with:

```css
.trend-panel,
.history-panel,
.insights-panel {
  padding: 14px;
}
```

- [ ] **Step 3: Add the larger chart frame**

After the existing `.trend-chart-frame` rule, add:

```css
.trend-chart-frame--long {
  height: 300px;
}
```

- [ ] **Step 4: Add the insights metric grid**

After the existing `.metric-strip` rules, add:

```css
.insight-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px;
  margin-top: 12px;
}

.insight-grid article {
  min-width: 0;
  padding: 12px;
}

.insight-grid span,
.insight-grid strong {
  display: block;
  overflow-wrap: anywhere;
}

.insight-grid span {
  margin-bottom: 5px;
  color: var(--muted);
  font-size: 0.72rem;
}

.insight-grid strong {
  font-size: 1rem;
  line-height: 1.15;
}
```

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test WeightTracker.sln --filter "Dashboard_RendersDeepInsightSectionsWithAllTimeData|Dashboard_WithNoEntries_RendersEmptyDeepInsights"
```

Expected: PASS.

- [ ] **Step 6: Commit styling**

```powershell
git add src/WeightTracker.Web/wwwroot/css/site.css
git commit -m "style: add dashboard insight sections"
```

---

### Task 5: Full Verification

**Files:**
- Verify: all modified source and test files.

- [ ] **Step 1: Run the full test suite**

Run:

```powershell
dotnet test WeightTracker.sln
```

Expected: all tests pass.

- [ ] **Step 2: Inspect the final diff**

Run:

```powershell
git diff origin/development...HEAD --stat
git diff origin/development...HEAD -- tests/WeightTracker.Tests/DashboardPageTests.cs src/WeightTracker.Web/Pages/Index.cshtml.cs src/WeightTracker.Web/Pages/Index.cshtml src/WeightTracker.Web/wwwroot/css/site.css
```

Expected: only dashboard deep insight test, model, markup, script, and CSS changes appear. No new History page, no full history table, no new modals, no new dependencies.

- [ ] **Step 3: Confirm working tree status**

Run:

```powershell
git status --short --branch
```

Expected: clean working tree on the implementation branch after the task commits.

---

## Plan Self-Review

- Spec coverage: covers single-page dashboard, preserved top section, all-entry long-term chart, focused metrics, no full history table, no new modals, no sticky UI, and no new dependencies.
- Placeholder scan: no `TBD`, `TODO`, or vague implementation steps.
- Type consistency: uses existing `ChartSeries`, `MetricsSummary`, `DashboardHistoryRow`, `FormatWeight`, and `FormatSignedWeight` names consistently.
- Scope check: does not add a History page, settings UI, CSV, deployment, migrations, AJAX, or client-side routing.
