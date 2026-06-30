# Dashboard Responsive Layout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the dashboard use full desktop width with a responsive multi-column layout while keeping the mobile dashboard as a scrollable stack.

**Architecture:** Add light semantic grouping to the existing Razor dashboard and drive layout through mobile-first CSS. Keep all existing data loading, dialogs, Chart.js behavior, form handlers, and dashboard sections intact.

**Tech Stack:** ASP.NET Core Razor Pages, xUnit integration tests, CSS Grid, Chart.js.

---

## File Structure

- Modify `tests/WeightTracker.Tests/DashboardPageTests.cs`: add non-brittle HTML assertions for the new dashboard grouping classes and keep existing section assertions.
- Modify `src/WeightTracker.Web/Pages/Index.cshtml`: add three wrapper `div` elements around existing sections without duplicating content.
- Modify `src/WeightTracker.Web/wwwroot/css/site.css`: add mobile-first layout group styles, tablet widening, desktop grid placement, desktop chart height, and an ultra-wide max width.

No service, model, database, JavaScript, or dependency changes are planned.

---

### Task 1: Add Failing HTML Structure Test

**Files:**
- Modify: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Update the dashboard render test with grouping assertions**

In `Dashboard_RendersMobileDashboardWithCalendarEntryDialog`, add these assertions after the existing `Assert.Contains("class=\"weight-app\"", html);` line:

```csharp
Assert.Contains("class=\"dashboard-summary\"", html);
Assert.Contains("class=\"dashboard-primary\"", html);
Assert.Contains("class=\"dashboard-supporting\"", html);
Assert.Equal(1, Regex.Matches(html, "class=\"dashboard-summary\"").Count);
Assert.Equal(1, Regex.Matches(html, "class=\"dashboard-primary\"").Count);
Assert.Equal(1, Regex.Matches(html, "class=\"dashboard-supporting\"").Count);
```

- [ ] **Step 2: Run the targeted failing test**

Run:

```powershell
dotnet test WeightTracker.sln --filter "FullyQualifiedName~Dashboard_RendersMobileDashboardWithCalendarEntryDialog"
```

Expected: FAIL because `dashboard-summary`, `dashboard-primary`, and `dashboard-supporting` do not exist yet.

- [ ] **Step 3: Commit the failing test**

```powershell
git add tests\WeightTracker.Tests\DashboardPageTests.cs
git commit -m "Add dashboard layout grouping test"
```

---

### Task 2: Add Semantic Dashboard Groups

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml`
- Test: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Wrap summary sections**

In `src/WeightTracker.Web/Pages/Index.cshtml`, immediately inside `<div class="weight-app">`, wrap the existing header, latest weight section, goal section, and metric strip with:

```cshtml
<div class="dashboard-summary">
    <header class="app-header">
        <div>
            <p class="eyebrow">WeightTracker</p>
            <h1>Dashboard</h1>
        </div>
        <span class="app-header__date">@Model.Today.ToString("dd MMM")</span>
    </header>

    <section class="weight-hero" aria-label="Latest weight">
        <div>
            <p class="eyebrow">Latest weight</p>
            <strong class="weight-hero__value">@Model.FormatWeight(Model.Summary.LatestWeightKg)</strong>
        </div>
        <button type="button" class="primary-action" data-open-entry>Add / Update</button>
    </section>

    <section class="goal-panel" aria-label="Goal">
        <div>
            <p class="eyebrow">Goal</p>
            <strong class="goal-panel__value">@(Model.HasGoal ? Model.FormatWeight(Model.Summary.GoalWeightKg) : "No goal set")</strong>
            <span>@Model.FormatGoalPanelDetail()</span>
        </div>
        <button type="button" class="trophy-button" data-open-goal aria-label="@Model.GoalActionLabel">&#127942;</button>
    </section>

    <section class="metric-strip" aria-label="Weight metrics">
        <article>
            <span>7-day avg</span>
            <strong>@Model.FormatWeight(Model.Summary.SevenDayMovingAverageKg)</strong>
        </article>
        <article>
            <span>Week delta</span>
            <strong>@Model.FormatSignedWeight(Model.Summary.WeekOverWeekDeltaKg)</strong>
        </article>
        <article>
            <span>@(Model.Summary.GoalWeightKg.HasValue ? "Goal gap" : "30-day")</span>
            <strong>@Model.FormatGoalDistance()</strong>
        </article>
    </section>
</div>
```

- [ ] **Step 2: Wrap the Trend panel**

Wrap the existing Trend section with:

```cshtml
<div class="dashboard-primary">
    <section class="trend-panel" aria-label="Weight trend">
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
        <div class="trend-chart-frame">
            <canvas id="trendChart"></canvas>
        </div>
    </section>
</div>
```

- [ ] **Step 3: Wrap supporting panels**

Insert this opening wrapper immediately before the existing `<section class="history-panel" aria-label="Recent history">` line:

```cshtml
<div class="dashboard-supporting">
```

Insert this closing wrapper immediately after the existing `</section>` that closes `<section class="data-panel" aria-label="Data management">`:

```cshtml
</div>
```

Keep the existing Recent history, Insights, and Data section contents unchanged inside the wrapper. Do not move dialogs into the wrapper; dialogs stay after the closing `</div>` for `.weight-app`.

- [ ] **Step 4: Run the targeted test**

Run:

```powershell
dotnet test WeightTracker.sln --filter "FullyQualifiedName~Dashboard_RendersMobileDashboardWithCalendarEntryDialog"
```

Expected: PASS.

- [ ] **Step 5: Commit the markup**

```powershell
git add src\WeightTracker.Web\Pages\Index.cshtml tests\WeightTracker.Tests\DashboardPageTests.cs
git commit -m "Group dashboard layout sections"
```

---

### Task 3: Add Responsive Layout CSS

**Files:**
- Modify: `src/WeightTracker.Web/wwwroot/css/site.css`

- [ ] **Step 1: Add base group styles**

After the existing `.weight-app` block, add:

```css
.dashboard-summary,
.dashboard-primary,
.dashboard-supporting {
  min-width: 0;
  display: grid;
  gap: 12px;
}
```

- [ ] **Step 2: Add tablet layout rules**

Before the existing `@media (max-width: 420px)` block, add:

```css
@media (min-width: 768px) {
  .app-shell {
    padding: 32px 24px;
  }

  .validation-summary,
  .status-message,
  .weight-app {
    max-width: 860px;
  }

  .weight-app {
    padding: 20px;
    gap: 16px;
  }

  .dashboard-summary,
  .dashboard-primary,
  .dashboard-supporting {
    gap: 16px;
  }

  .insight-grid {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .data-actions {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .primary-action {
    width: auto;
  }
}
```

- [ ] **Step 3: Add desktop layout rules**

After the tablet media query, add:

```css
@media (min-width: 1024px) {
  .app-shell {
    align-items: stretch;
    padding: 36px 28px;
  }

  .validation-summary,
  .status-message,
  .weight-app {
    max-width: none;
  }

  .weight-app {
    grid-template-columns: minmax(280px, 0.85fr) minmax(0, 2.15fr);
    align-items: start;
    width: 100%;
  }

  .dashboard-summary {
    grid-column: 1;
    align-self: start;
  }

  .dashboard-primary {
    grid-column: 2;
    min-height: 100%;
  }

  .dashboard-primary .trend-panel {
    min-height: 100%;
  }

  .dashboard-supporting {
    grid-column: 1 / -1;
    grid-template-columns: minmax(260px, 0.9fr) minmax(0, 1.35fr) minmax(240px, 0.75fr);
    align-items: start;
  }

  .trend-chart-frame {
    height: 320px;
    margin-top: 10px;
  }

  .metric-strip {
    grid-template-columns: 1fr;
  }

  .data-actions {
    grid-template-columns: 1fr;
  }
}
```

- [ ] **Step 4: Add ultra-wide cap**

After the desktop media query, add:

```css
@media (min-width: 1280px) {
  .validation-summary,
  .status-message,
  .weight-app {
    max-width: 1240px;
  }
}
```

- [ ] **Step 5: Run the full test suite**

Run:

```powershell
dotnet test WeightTracker.sln
```

Expected: PASS.

- [ ] **Step 6: Commit the responsive CSS**

```powershell
git add src\WeightTracker.Web\wwwroot\css\site.css
git commit -m "Add responsive dashboard layout"
```

---

### Task 4: Browser Verification And Polish

**Files:**
- Modify if needed: `src/WeightTracker.Web/wwwroot/css/site.css`

- [ ] **Step 1: Start the app locally**

Run:

```powershell
dotnet run --project src\WeightTracker.Web\WeightTracker.Web.csproj --urls http://localhost:18080
```

Expected: the app starts and listens on `http://localhost:18080`.

- [ ] **Step 2: Check mobile viewport**

Open `http://localhost:18080` at approximately `390x844`.

Expected:

- Dashboard remains a single scrollable stack.
- Section order is unchanged.
- The Add / Update button, trophy button, trend range selector, and Data actions do not overlap.
- The chart canvas is visible and nonblank.

- [ ] **Step 3: Check tablet viewport**

Open the same page at approximately `820x1180`.

Expected:

- Dashboard uses more width than the old phone card.
- Layout remains readable and mostly stacked.
- Insights and Data actions use wider grids.
- No horizontal page scroll appears.

- [ ] **Step 4: Check desktop viewport**

Open the same page at approximately `1440x1000`.

Expected:

- Summary column appears beside the Trend panel.
- Trend chart is wider and at least 300px tall.
- Recent history, Insights, and Data appear as supporting panels below the top row.
- Dialogs still fit the viewport.

- [ ] **Step 5: Apply minimal CSS polish if verification finds layout defects**

Use only targeted CSS changes. Examples:

```css
.trend-heading {
  flex-wrap: wrap;
}

.dashboard-supporting > * {
  min-width: 0;
}
```

Do not change server-side behavior or add JavaScript unless the chart remains blank after resize.

- [ ] **Step 6: Run final verification**

Run:

```powershell
dotnet test WeightTracker.sln
```

Expected: PASS.

- [ ] **Step 7: Commit verification polish if any files changed**

If Step 5 changed CSS, run:

```powershell
git add src\WeightTracker.Web\wwwroot\css\site.css
git commit -m "Polish dashboard responsive layout"
```

If no files changed, do not create an empty commit.

---

## Self-Review Notes

- Spec coverage: tasks cover semantic grouping, mobile-first CSS, tablet widening, desktop multi-column layout, desktop chart height, ultra-wide cap, tests, and browser verification.
- Placeholder scan: no task uses deferred implementation markers or unspecified test work.
- Type consistency: class names are consistently `dashboard-summary`, `dashboard-primary`, and `dashboard-supporting`.
