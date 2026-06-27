# Dashboard Mobile Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the editable date-card feed with a focused mobile-style dashboard and calendar-based entry panel.

**Architecture:** Keep the existing Razor Pages endpoint and persistence services. Add page-model view data for recent history, current-month calendar days, and entry lookup data, then render a read-only dashboard plus a dialog-based entry form that posts the selected date through existing handlers.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core, xUnit, Bootstrap runtime assets already present, Chart.js already loaded.

---

### Task 1: Dashboard Contract Tests

**Files:**
- Modify: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Write failing tests**

Add tests asserting that the dashboard renders a phone app shell, a single entry dialog, current-month calendar buttons, compact recent history, and no editable card feed.

- [ ] **Step 2: Run focused tests**

Run: `dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --no-restore --filter DashboardPageTests`

Expected: fail because the current page still renders `entry-card` date forms and no calendar entry dialog.

### Task 2: Page Model Data

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml.cs`

- [ ] **Step 1: Implement minimal view records**

Add records for recent history rows and current-month calendar days. Load entries from the start of the visible month through today, plus the existing chart range.

- [ ] **Step 2: Keep post behavior stable**

Continue using `EntryDate` and `Weight` bind properties so existing save/delete flows remain compatible.

- [ ] **Step 3: Run focused tests**

Run: `dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --no-restore --filter DashboardPageTests`

Expected: model compiles, markup tests still fail until Task 3.

### Task 3: Dashboard Markup And Script

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml`

- [ ] **Step 1: Replace card feed**

Render a `weight-app` shell with current weight hero, metric strip, trend chart, recent history rows, and a primary button opening a dialog.

- [ ] **Step 2: Add calendar dialog**

Render current-month day buttons. Disable future days. Store selected date in a hidden `EntryDate` input and load existing day weights from embedded JSON.

- [ ] **Step 3: Preserve validation**

Keep the validation summary, decimal input cleanup, and antiforgery form behavior.

- [ ] **Step 4: Run focused tests**

Run: `dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --no-restore --filter DashboardPageTests`

Expected: dashboard tests pass or expose small markup mismatches.

### Task 4: Mobile Dark Styling

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Shared/_Layout.cshtml`
- Modify: `src/WeightTracker.Web/wwwroot/css/site.css`

- [ ] **Step 1: Simplify layout chrome**

Render a minimal app layout without the default Bootstrap navbar/footer clutter.

- [ ] **Step 2: Add restrained dark/cyan style**

Style the dashboard as a centered phone-width app surface with readable dark contrast, compact cards, and a cyan primary action.

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/WeightTracker.Tests/WeightTracker.Tests.csproj --no-restore`

Expected: all tests pass.

### Task 5: Final Verification

**Files:**
- Review all changed files.

- [ ] **Step 1: Run full test suite**

Run: `dotnet test WeightTracker.sln`

Expected: all tests pass.

- [ ] **Step 2: Inspect git diff**

Run: `git diff --stat` and review changed files for unrelated churn.
