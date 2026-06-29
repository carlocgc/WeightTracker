# Dashboard Trend And Data Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the duplicated dashboard trend charts with one range-selectable chart and simplify the Data section to three actions with CSV import in a modal.

**Architecture:** Keep the existing Razor Pages dashboard and Chart.js dependency. Render all chart data once from the existing all-entry chart series, then filter the single chart client-side when the user selects a range. Keep CSV export and import handlers unchanged; only move the import file input into a dialog.

**Tech Stack:** ASP.NET Core Razor Pages, C#, xUnit, Chart.js, HTML dialog element, CSS.

---

## File Structure

- Modify `tests/WeightTracker.Tests/DashboardPageTests.cs`: update dashboard rendering tests for one chart, range controls, and import modal structure.
- Modify `src/WeightTracker.Web/Pages/Index.cshtml`: remove the long-term chart panel, add range selector buttons, simplify Data actions, add import dialog, and update chart JavaScript.
- Modify `src/WeightTracker.Web/wwwroot/css/site.css`: add range selector styling, normalize action button alignment, add import dialog support, and remove long-chart-only styling if unused.

## Task 1: Test Combined Trend Chart Rendering

**Files:**
- Modify: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Update the deep insights dashboard test before production code**

In `Dashboard_RendersDeepInsightSectionsWithAllTimeData`, replace the long-term chart assertions with combined chart assertions:

```csharp
Assert.DoesNotContain("Long-term trend", html);
Assert.DoesNotContain("id=\"longRangeTrendChart\"", html);
Assert.Contains("data-trend-range=\"1m\"", html);
Assert.Contains("data-trend-range=\"3m\"", html);
Assert.Contains("data-trend-range=\"6m\"", html);
Assert.Contains("data-trend-range=\"1y\"", html);
Assert.Contains("data-trend-range=\"all\"", html);
Assert.Contains("aria-pressed=\"true\">6M</button>", html);
Assert.Contains("\"date\":\"2025-11-01\"", html);
```

Keep the existing Insights and metric assertions in the same test.

- [ ] **Step 2: Update the no-entries dashboard test before production code**

In `Dashboard_WithNoEntries_RendersEmptyDeepInsights`, replace the old long-term chart assertions:

```csharp
Assert.DoesNotContain("Long-term trend", html);
Assert.DoesNotContain("id=\"longRangeTrendChart\"", html);
Assert.Contains("Insights", html);
Assert.Contains("Entry count", html);
Assert.Contains(">0</strong>", html);
Assert.Contains("No weights recorded yet.", html);
```

- [ ] **Step 3: Run tests to verify RED**

Run:

```powershell
dotnet test WeightTracker.sln --filter "FullyQualifiedName~DashboardPageTests"
```

Expected: FAIL because the current dashboard still renders `Long-term trend`, `longRangeTrendChart`, and no range selector buttons.

## Task 2: Test Data Section Cleanup And Import Modal

**Files:**
- Modify: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Update data management rendering test before production code**

In `Dashboard_RendersDataManagementSection`, assert the simplified Data section and import modal:

```csharp
Assert.Contains("aria-label=\"Data management\"", html);
Assert.Contains("href=\"/?handler=ExportCsv\"", html);
Assert.Contains("data-open-import", html);
Assert.Contains("id=\"importCsvDialog\"", html);
Assert.Contains("name=\"ImportFile\"", html);
Assert.Contains("Delete all", html);
Assert.Contains("id=\"deleteAllWarningDialog\"", html);
Assert.Contains("id=\"deleteAllConfirmDialog\"", html);
Assert.DoesNotContain("class=\"data-upload\"", html);
Assert.DoesNotContain("for=\"importFile\">Import CSV</label>", html);
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```powershell
dotnet test WeightTracker.sln --filter "FullyQualifiedName~DashboardPageTests"
```

Expected: FAIL because import still renders inline in `.data-upload` and there is no `importCsvDialog`.

## Task 3: Implement Dashboard Markup And Script

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml`

- [ ] **Step 1: Replace Trend heading with range selector**

Change the Trend panel heading to include buttons:

```html
<div class="section-heading trend-heading">
    <div>
        <h2>Trend</h2>
        <span data-trend-range-label>Last 6 months</span>
    </div>
    <div class="trend-range-selector" aria-label="Trend time frame">
        <button type="button" data-trend-range="1m" data-range-label="Last month" aria-pressed="false">1M</button>
        <button type="button" data-trend-range="3m" data-range-label="Last 3 months" aria-pressed="false">3M</button>
        <button type="button" data-trend-range="6m" data-range-label="Last 6 months" aria-pressed="true">6M</button>
        <button type="button" data-trend-range="1y" data-range-label="Last year" aria-pressed="false">1Y</button>
        <button type="button" data-trend-range="all" data-range-label="All entries" aria-pressed="false">All</button>
    </div>
</div>
```

- [ ] **Step 2: Remove the long-term trend section**

Delete the entire section with:

```html
<section class="trend-panel trend-panel--long" aria-label="Long-term trend">
```

through its closing `</section>`.

- [ ] **Step 3: Simplify Data actions**

Replace the inline import form in `.data-actions` with:

```html
<a class="primary-action action-button" asp-page-handler="ExportCsv">Export CSV</a>
<button type="button" class="primary-action action-button" data-open-import>Import CSV</button>
<button type="button" class="secondary-action action-button" data-open-delete-all>Delete all</button>
```

- [ ] **Step 4: Add import dialog before delete-all dialogs**

Insert:

```html
<dialog id="importCsvDialog" class="import-dialog" role="dialog" aria-modal="true" aria-labelledby="importCsvDialogTitle">
    <form method="post" enctype="multipart/form-data" asp-page-handler="ImportCsv" class="entry-dialog__form">
        <div class="entry-dialog__header">
            <div>
                <p class="eyebrow">Import</p>
                <h2 id="importCsvDialogTitle">Import CSV</h2>
            </div>
            <button type="button" class="icon-button" data-close-import aria-label="Close">x</button>
        </div>
        <label class="weight-input-label" for="importFile">CSV file</label>
        <input id="importFile" class="file-input" type="file" name="ImportFile" accept=".csv,text/csv" />
        <div class="entry-dialog__actions">
            <button type="submit" class="primary-action">Import CSV</button>
            <button type="button" class="secondary-action secondary-action--neutral" data-close-import>Cancel</button>
        </div>
    </form>
</dialog>
```

- [ ] **Step 5: Update dialog JavaScript**

Add `importDialog` and import open/close listeners near the existing delete-all dialog constants:

```javascript
const importDialog = document.getElementById('importCsvDialog');

document.querySelectorAll('[data-open-import]').forEach((button) => {
    button.addEventListener('click', () => showDialog(importDialog));
});

document.querySelectorAll('[data-close-import]').forEach((button) => {
    button.addEventListener('click', () => closeDialog(importDialog));
});
```

- [ ] **Step 6: Replace chart rendering with one reusable chart instance**

Replace `renderTrendChart` and the two chart calls with range-filtered rendering:

```javascript
function parseDateOnly(value) {
    const parts = value.split('-').map(Number);
    return new Date(parts[0], parts[1] - 1, parts[2]);
}

function addMonths(date, months) {
    return new Date(date.getFullYear(), date.getMonth() + months, date.getDate());
}

function rangeStartDate(range, endDate) {
    if (range === 'all') {
        return null;
    }

    const monthsByRange = { '1m': -1, '3m': -3, '6m': -6, '1y': -12 };
    return addMonths(endDate, monthsByRange[range] || -6);
}

function filterPoints(points, startDate) {
    if (!startDate) {
        return points;
    }

    return points.filter((point) => parseDateOnly(point.date) >= startDate);
}

function buildTrendData(dailyPoints, movingPoints, goalWeight, range) {
    const endDate = parseDateOnly(today);
    const startDate = rangeStartDate(range, endDate);
    const filteredDaily = filterPoints(dailyPoints, startDate);
    const filteredMoving = filterPoints(movingPoints, startDate);
    const labels = filteredDaily.map((point) => point.date);

    return {
        labels,
        datasets: [
            { label: 'Daily', data: filteredDaily.map((point) => point.weightKg), borderColor: '#28f0d4', backgroundColor: 'rgba(40, 240, 212, 0.12)', tension: 0.35, pointRadius: 2 },
            { label: '7-day avg', data: filteredMoving.map((point) => point.weightKg), borderColor: '#f8c14a', tension: 0.35, pointRadius: 0 },
            { label: 'Goal', data: goalWeight ? labels.map(() => goalWeight) : [], borderColor: '#85e89d', borderDash: [6, 6], pointRadius: 0 }
        ]
    };
}

function createTrendChart(canvasId, dailyPoints, movingPoints, goalWeight) {
    const canvas = document.getElementById(canvasId);
    if (!canvas || !window.Chart) {
        return null;
    }

    return new Chart(canvas, {
        type: 'line',
        data: buildTrendData(dailyPoints, movingPoints, goalWeight, '6m'),
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { display: false } },
            scales: {
                x: { grid: { color: 'rgba(255,255,255,0.04)' }, ticks: { color: '#7d8798', maxTicksLimit: 5 } },
                y: { grid: { color: 'rgba(255,255,255,0.06)' }, ticks: { color: '#7d8798' } }
            }
        }
    });
}
```

Then wire buttons:

```javascript
const trendDaily = @Json.Serialize(Model.LongRangeChart.DailyWeights);
const trendMoving = @Json.Serialize(Model.LongRangeChart.MovingAverages);
const trendGoal = @Json.Serialize(Model.LongRangeChart.GoalWeightKg);
const trendChart = createTrendChart('trendChart', trendDaily, trendMoving, trendGoal);
const trendRangeLabel = document.querySelector('[data-trend-range-label]');

document.querySelectorAll('[data-trend-range]').forEach((button) => {
    button.addEventListener('click', () => {
        if (!trendChart) {
            return;
        }

        document.querySelectorAll('[data-trend-range]').forEach((rangeButton) => {
            rangeButton.setAttribute('aria-pressed', rangeButton === button ? 'true' : 'false');
        });

        trendChart.data = buildTrendData(trendDaily, trendMoving, trendGoal, button.dataset.trendRange);
        trendChart.update();

        if (trendRangeLabel) {
            trendRangeLabel.textContent = button.dataset.rangeLabel;
        }
    });
});
```

- [ ] **Step 7: Run targeted tests to verify GREEN for markup**

Run:

```powershell
dotnet test WeightTracker.sln --filter "FullyQualifiedName~DashboardPageTests"
```

Expected: PASS for dashboard page tests.

## Task 4: Implement Styling

**Files:**
- Modify: `src/WeightTracker.Web/wwwroot/css/site.css`

- [ ] **Step 1: Add action alignment and range selector CSS**

Add:

```css
.action-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  text-align: center;
}

.trend-heading {
  align-items: flex-start;
}

.trend-range-selector {
  display: inline-grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 3px;
  padding: 3px;
  border: 1px solid var(--line);
  border-radius: 999px;
  background: #0d121b;
}

.trend-range-selector button {
  min-width: 32px;
  min-height: 30px;
  padding: 0 8px;
  border: 0;
  border-radius: 999px;
  background: transparent;
  color: var(--muted);
  cursor: pointer;
  font-size: 0.72rem;
  font-weight: 800;
}

.trend-range-selector button[aria-pressed="true"] {
  background: var(--accent);
  color: #06110f;
}
```

- [ ] **Step 2: Include import dialog in dialog base styles**

Change:

```css
.entry-dialog,
.goal-dialog,
.delete-dialog {
```

to:

```css
.entry-dialog,
.goal-dialog,
.import-dialog,
.delete-dialog {
```

Do the same for the `::backdrop` rule.

- [ ] **Step 3: Remove unused long chart height rule**

Delete:

```css
.trend-chart-frame--long {
  height: 300px;
}
```

- [ ] **Step 4: Run dashboard tests again**

Run:

```powershell
dotnet test WeightTracker.sln --filter "FullyQualifiedName~DashboardPageTests"
```

Expected: PASS.

## Task 5: Full Verification And Review

**Files:**
- Read/review all modified files.

- [ ] **Step 1: Run full test suite**

Run:

```powershell
dotnet test WeightTracker.sln
```

Expected: PASS.

- [ ] **Step 2: Review diff**

Run:

```powershell
git diff -- src/WeightTracker.Web/Pages/Index.cshtml src/WeightTracker.Web/wwwroot/css/site.css tests/WeightTracker.Tests/DashboardPageTests.cs
```

Confirm the diff is scoped to the approved dashboard chart and data-section cleanup.

- [ ] **Step 3: Check status**

Run:

```powershell
git status --short --branch
```

Expected: modified files limited to the implementation plan, dashboard page, CSS, and dashboard tests.
