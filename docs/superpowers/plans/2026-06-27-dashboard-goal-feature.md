# Dashboard Goal Feature Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a dashboard Goal panel with a trophy-triggered modal so users can set, update, and clear their optional goal weight without leaving `/`.

**Architecture:** Keep `IndexModel` as the dashboard owner and reuse the existing singleton `AppSettings.GoalWeightKg`. Add dedicated POST handlers for saving and clearing the goal, render a compact Goal panel below the latest-weight hero, and reuse the existing `<dialog>`/client script style for modal behavior and validation reopening.

**Tech Stack:** ASP.NET Core Razor Pages, EF Core-backed settings service, xUnit integration tests, existing CSS and browser `<dialog>` APIs.

---

## File Structure

- Modify `tests/WeightTracker.Tests/DashboardPageTests.cs`: add integration tests for no-goal rendering, existing-goal rendering, saving, validation reopening, clearing, and preserving other settings.
- Modify `src/WeightTracker.Web/Pages/Index.cshtml.cs`: add goal bind property, goal modal state, formatting/input helpers, and `OnPostGoalAsync` / `OnPostClearGoalAsync` handlers.
- Modify `src/WeightTracker.Web/Pages/Index.cshtml`: render the Goal panel below the latest-weight hero, add the goal dialog, and wire client-side open/close/focus behavior without interfering with the entry dialog.
- Modify `src/WeightTracker.Web/wwwroot/css/site.css`: style the Goal panel, trophy icon button, goal dialog input, and responsive behavior using the existing compact dark dashboard language.

---

### Task 1: Add Failing Goal Feature Tests

**Files:**
- Modify: `tests/WeightTracker.Tests/DashboardPageTests.cs`

- [ ] **Step 1: Add rendering and behavior tests**

Add these tests after `Dashboard_WithNoEntries_RendersEmptyDeepInsights`:

```csharp
[Fact]
public async Task Dashboard_WithNoGoal_RendersGoalPanelAndSetAction()
{
    await using var app = new DashboardTestApp();
    await app.UpdateSettingsAsync("kg");
    var client = app.CreateClient();

    var response = await client.GetAsync("/");
    var html = await response.Content.ReadAsStringAsync();

    Assert.True(response.StatusCode == HttpStatusCode.OK, html);
    Assert.Contains("aria-label=\"Goal\"", html);
    Assert.Contains("No goal set", html);
    Assert.Contains("Set a target weight", html);
    Assert.Contains("aria-label=\"Set goal\"", html);
    Assert.Contains("id=\"goalDialog\"", html);
    Assert.Contains("name=\"GoalWeight\"", html);
    Assert.DoesNotContain("Clear goal", html);
}

[Fact]
public async Task Dashboard_WithGoal_RendersGoalPanelAndEditAction()
{
    await using var app = new DashboardTestApp();
    await app.UpdateSettingsAsync("kg", goalWeightKg: 78m);
    await app.AddEntryAsync(Today, 82.1m);
    var client = app.CreateClient();

    var response = await client.GetAsync("/");
    var html = await response.Content.ReadAsStringAsync();

    Assert.True(response.StatusCode == HttpStatusCode.OK, html);
    Assert.Contains("aria-label=\"Goal\"", html);
    Assert.Contains("78.0 kg", html);
    Assert.Contains("+4.1 kg", html);
    Assert.Contains("aria-label=\"Edit goal\"", html);
    Assert.Contains("value=\"78.0\"", html);
    Assert.Contains("Clear goal", html);
}

[Fact]
public async Task SaveGoal_WithSelectedDisplayUnit_PersistsConvertedGoal()
{
    await using var app = new DashboardTestApp();
    await app.UpdateSettingsAsync("lb", weekStartsOn: DayOfWeek.Sunday, timeZoneId: "Europe/London", theme: "light");
    var client = app.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });
    var token = await GetRequestVerificationTokenAsync(client);

    var response = await client.PostAsync("/?handler=Goal", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token,
        ["GoalWeight"] = "180"
    }));

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    var settings = await app.GetSettingsAsync();
    Assert.Equal("lb", settings.DisplayUnit);
    Assert.Equal(81.647m, settings.GoalWeightKg);
    Assert.Equal(DayOfWeek.Sunday, settings.WeekStartsOn);
    Assert.Equal("Europe/London", settings.TimeZoneId);
    Assert.Equal("light", settings.Theme);
}

[Fact]
public async Task SaveGoal_WithInvalidGoal_ReturnsValidationAndReopensGoalDialog()
{
    await using var app = new DashboardTestApp();
    await app.UpdateSettingsAsync("kg");
    var client = app.CreateClient();
    var token = await GetRequestVerificationTokenAsync(client);

    var response = await client.PostAsync("/?handler=Goal", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token,
        ["GoalWeight"] = "0"
    }));
    var html = await response.Content.ReadAsStringAsync();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.Contains("Enter a goal greater than zero.", html);
    Assert.Contains("data-open-goal-on-load=\"true\"", html);
    Assert.Null((await app.GetSettingsAsync()).GoalWeightKg);
}

[Fact]
public async Task ClearGoal_RemovesGoalAndPreservesOtherSettings()
{
    await using var app = new DashboardTestApp();
    await app.UpdateSettingsAsync("lb", goalWeightKg: 80m, weekStartsOn: DayOfWeek.Sunday, timeZoneId: "Europe/London", theme: "light");
    var client = app.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false
    });
    var token = await GetRequestVerificationTokenAsync(client);

    var response = await client.PostAsync("/?handler=ClearGoal", new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["__RequestVerificationToken"] = token
    }));

    Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    var settings = await app.GetSettingsAsync();
    Assert.Equal("lb", settings.DisplayUnit);
    Assert.Null(settings.GoalWeightKg);
    Assert.Equal(DayOfWeek.Sunday, settings.WeekStartsOn);
    Assert.Equal("Europe/London", settings.TimeZoneId);
    Assert.Equal("light", settings.Theme);
}
```

- [ ] **Step 2: Replace the test helper settings method and add a settings getter**

In the nested `DashboardTestApp`, replace the existing `UpdateSettingsAsync` helper:

```csharp
public async Task UpdateSettingsAsync(string displayUnit)
{
    using var scope = Services.CreateScope();
    var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
    await settings.UpdateAsync(displayUnit, null, DayOfWeek.Monday, "Europe/London", "dark");
}
```

with:

```csharp
public async Task UpdateSettingsAsync(
    string displayUnit,
    decimal? goalWeightKg = null,
    DayOfWeek weekStartsOn = DayOfWeek.Monday,
    string timeZoneId = "Europe/London",
    string theme = "dark")
{
    using var scope = Services.CreateScope();
    var settings = scope.ServiceProvider.GetRequiredService<SettingsService>();
    await settings.UpdateAsync(displayUnit, goalWeightKg, weekStartsOn, timeZoneId, theme);
}

public async Task<AppSettings> GetSettingsAsync()
{
    using var scope = Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WeightTrackerDbContext>();
    return await db.AppSettings.AsNoTracking().SingleAsync(settings => settings.Id == AppSettings.SingletonId);
}
```

- [ ] **Step 3: Run focused tests and verify they fail**

Run:

```powershell
dotnet test WeightTracker.sln --filter "Dashboard_WithNoGoal_RendersGoalPanelAndSetAction|Dashboard_WithGoal_RendersGoalPanelAndEditAction|SaveGoal_WithSelectedDisplayUnit_PersistsConvertedGoal|SaveGoal_WithInvalidGoal_ReturnsValidationAndReopensGoalDialog|ClearGoal_RemovesGoalAndPreservesOtherSettings"
```

Expected: tests compile, then fail because the dashboard does not yet render the Goal panel/dialog and does not have `Goal` or `ClearGoal` handlers.

- [ ] **Step 4: Commit failing tests**

```powershell
git add tests/WeightTracker.Tests/DashboardPageTests.cs
git commit -m "test: cover dashboard goal feature"
```

---

### Task 2: Add Goal State And POST Handlers To IndexModel

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml.cs`

- [ ] **Step 1: Add goal bind and modal properties**

In `IndexModel`, after the existing `Weight` bind property, add:

```csharp
[BindProperty]
public decimal? GoalWeight { get; set; }
```

After the existing `EntryCount` property, add:

```csharp
public bool GoalDialogOpen { get; private set; }

public string GoalDialogOpenAttribute => GoalDialogOpen ? "true" : "false";
```

- [ ] **Step 2: Add goal POST handlers**

After `OnPostDeleteAsync`, add:

```csharp
public async Task<IActionResult> OnPostGoalAsync(CancellationToken cancellationToken)
{
    if (GoalWeight is null or <= 0)
    {
        ModelState.AddModelError(nameof(GoalWeight), "Enter a goal greater than zero.");
        GoalDialogOpen = true;
        await LoadAsync(cancellationToken);
        return Page();
    }

    var settings = await settingsService.GetAsync(cancellationToken);
    var goalWeightKg = decimal.Round(WeightConversionService.ToKilograms(GoalWeight.Value, settings.DisplayUnit), 3);
    await settingsService.UpdateAsync(
        settings.DisplayUnit,
        goalWeightKg,
        settings.WeekStartsOn,
        settings.TimeZoneId,
        settings.Theme,
        cancellationToken);

    return RedirectToPage();
}

public async Task<IActionResult> OnPostClearGoalAsync(CancellationToken cancellationToken)
{
    var settings = await settingsService.GetAsync(cancellationToken);
    await settingsService.UpdateAsync(
        settings.DisplayUnit,
        null,
        settings.WeekStartsOn,
        settings.TimeZoneId,
        settings.Theme,
        cancellationToken);

    return RedirectToPage();
}
```

- [ ] **Step 3: Add goal helper methods**

After `FormatGoalDistance()`, add:

```csharp
public bool HasGoal => Summary.GoalWeightKg.HasValue;

public string GoalActionLabel => HasGoal ? "Edit goal" : "Set goal";

public string FormatGoalPanelDetail()
{
    if (!Summary.GoalWeightKg.HasValue)
    {
        return "Set a target weight";
    }

    if (!Summary.LatestWeightKg.HasValue)
    {
        return "Waiting for your first weight";
    }

    return FormatSignedWeight(Summary.LatestWeightKg.Value - Summary.GoalWeightKg.Value);
}

public string GoalInputValue()
{
    if (GoalDialogOpen && GoalWeight.HasValue)
    {
        return GoalWeight.Value.ToString("0.0", CultureInfo.InvariantCulture);
    }

    return InputWeightValue(Summary.GoalWeightKg);
}
```

- [ ] **Step 4: Populate goal input during load**

At the end of `LoadAsync`, immediately after:

```csharp
EntryCount = entries.Count;
```

add:

```csharp
if (!GoalDialogOpen)
{
    GoalWeight = Summary.GoalWeightKg.HasValue
        ? decimal.Round(WeightConversionService.FromKilograms(Summary.GoalWeightKg.Value, DisplayUnit), 1)
        : null;
}
```

This sets the initial value for normal page loads while leaving invalid posted values available to `GoalInputValue()` when validation reopens the modal.

- [ ] **Step 5: Run focused tests and verify handler tests pass or markup tests still fail**

Run:

```powershell
dotnet test WeightTracker.sln --filter "SaveGoal_WithSelectedDisplayUnit_PersistsConvertedGoal|SaveGoal_WithInvalidGoal_ReturnsValidationAndReopensGoalDialog|ClearGoal_RemovesGoalAndPreservesOtherSettings|Dashboard_WithNoGoal_RendersGoalPanelAndSetAction|Dashboard_WithGoal_RendersGoalPanelAndEditAction"
```

Expected: handler persistence tests compile. Rendering tests still fail because markup and script have not been added. The invalid goal test may still fail on `data-open-goal-on-load` until Task 3.

- [ ] **Step 6: Commit model handler changes**

```powershell
git add src/WeightTracker.Web/Pages/Index.cshtml.cs
git commit -m "feat: handle dashboard goal updates"
```

---

### Task 3: Render Goal Panel, Modal, And Script Wiring

**Files:**
- Modify: `src/WeightTracker.Web/Pages/Index.cshtml`

- [ ] **Step 1: Add the Goal panel below the latest-weight hero**

Immediately after the closing `</section>` for `.weight-hero`, add:

```cshtml
    <section class="goal-panel" aria-label="Goal">
        <div>
            <p class="eyebrow">Goal</p>
            <strong class="goal-panel__value">@(Model.HasGoal ? Model.FormatWeight(Model.Summary.GoalWeightKg) : "No goal set")</strong>
            <span>@Model.FormatGoalPanelDetail()</span>
        </div>
        <button type="button" class="trophy-button" data-open-goal aria-label="@Model.GoalActionLabel">&#127942;</button>
    </section>
```

- [ ] **Step 2: Add the goal dialog after the entry dialog**

Immediately after the existing `</dialog>` for `entryDialog`, add:

```cshtml
<dialog id="goalDialog" class="goal-dialog" role="dialog" aria-modal="true" aria-labelledby="goalDialogTitle" data-open-goal-on-load="@Model.GoalDialogOpenAttribute">
    <form method="post" class="entry-dialog__form" asp-page-handler="Goal">
        <div class="entry-dialog__header">
            <div>
                <p class="eyebrow">Goal</p>
                <h2 id="goalDialogTitle">Target weight</h2>
            </div>
            <button type="button" class="icon-button" data-close-goal aria-label="Close">x</button>
        </div>

        <label class="weight-input-label" for="goalWeightInput">Goal weight (@Model.DisplayUnit)</label>
        <input id="goalWeightInput"
               class="weight-input"
               name="GoalWeight"
               value="@Model.GoalInputValue()"
               type="text"
               inputmode="decimal"
               autocomplete="off"
               data-decimal-input
               aria-describedby="goal-validation" />
        <span id="goal-validation" asp-validation-for="GoalWeight"></span>

        <div class="entry-dialog__actions">
            <button type="submit" class="primary-action">Save</button>
            @if (Model.HasGoal)
            {
                <button type="submit" class="secondary-action" asp-page-handler="ClearGoal">Clear goal</button>
            }
            <button type="button" class="secondary-action secondary-action--neutral" data-close-goal>Cancel</button>
        </div>
    </form>
</dialog>
```

- [ ] **Step 3: Add goal dialog script variables**

In the script block, after:

```javascript
const entryDialog = document.getElementById('entryDialog');
const weightInput = document.getElementById('weightInput');
```

add:

```javascript
const goalDialog = document.getElementById('goalDialog');
const goalWeightInput = document.getElementById('goalWeightInput');
```

- [ ] **Step 4: Add goal dialog open/close behavior**

After the existing `data-close-entry` event listener block, add:

```javascript
document.querySelectorAll('[data-open-goal]').forEach((button) => {
    button.addEventListener('click', () => {
        if (goalDialog.showModal) {
            goalDialog.showModal();
        } else {
            goalDialog.setAttribute('open', 'open');
        }
        goalWeightInput.focus();
    });
});

document.querySelectorAll('[data-close-goal]').forEach((button) => {
    button.addEventListener('click', () => goalDialog.close());
});

if (goalDialog && goalDialog.dataset.openGoalOnLoad === 'true') {
    if (goalDialog.showModal) {
        goalDialog.showModal();
    } else {
        goalDialog.setAttribute('open', 'open');
    }
    goalWeightInput.focus();
}
```

- [ ] **Step 5: Run focused tests**

Run:

```powershell
dotnet test WeightTracker.sln --filter "Dashboard_WithNoGoal_RendersGoalPanelAndSetAction|Dashboard_WithGoal_RendersGoalPanelAndEditAction|SaveGoal_WithInvalidGoal_ReturnsValidationAndReopensGoalDialog"
```

Expected: PASS or fail only on exact markup text. If exact text differs, make the markup match the approved spec and test assertions above.

- [ ] **Step 6: Commit markup and script changes**

```powershell
git add src/WeightTracker.Web/Pages/Index.cshtml
git commit -m "feat: render dashboard goal modal"
```

---

### Task 4: Style The Goal Panel And Modal

**Files:**
- Modify: `src/WeightTracker.Web/wwwroot/css/site.css`

- [ ] **Step 1: Include goal surfaces in panel styling**

Replace:

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

with:

```css
.weight-hero,
.goal-panel,
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

- [ ] **Step 2: Add goal panel layout styles**

After the `.weight-hero__value` rule, add:

```css
.goal-panel {
  min-width: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px;
}

.goal-panel__value,
.goal-panel span {
  display: block;
  overflow-wrap: anywhere;
}

.goal-panel__value {
  font-size: 1.1rem;
  line-height: 1.15;
}

.goal-panel span {
  margin-top: 4px;
  color: var(--muted);
  font-size: 0.78rem;
}
```

- [ ] **Step 3: Style trophy and neutral buttons**

Replace:

```css
.primary-action,
.secondary-action,
.icon-button {
  border: 0;
  border-radius: 999px;
  cursor: pointer;
  text-decoration: none;
}
```

with:

```css
.primary-action,
.secondary-action,
.icon-button,
.trophy-button {
  border: 0;
  border-radius: 999px;
  cursor: pointer;
  text-decoration: none;
}
```

After the `.secondary-action` rule, add:

```css
.secondary-action--neutral {
  background: var(--surface-soft);
  color: var(--text);
  border-color: var(--line);
}

.trophy-button {
  width: 42px;
  height: 42px;
  display: inline-grid;
  place-items: center;
  flex: 0 0 auto;
  background: rgba(248, 193, 74, 0.13);
  color: var(--amber);
  border: 1px solid rgba(248, 193, 74, 0.34);
  font-size: 1.15rem;
  line-height: 1;
}
```

- [ ] **Step 4: Share dialog shell styling with the goal dialog**

Replace:

```css
.entry-dialog {
```

with:

```css
.entry-dialog,
.goal-dialog {
```

Replace:

```css
.entry-dialog::backdrop {
```

with:

```css
.entry-dialog::backdrop,
.goal-dialog::backdrop {
```

- [ ] **Step 5: Add responsive goal layout**

Inside the existing `@media (max-width: 420px)` block, after the `.weight-hero` rule, add:

```css
  .goal-panel {
    align-items: flex-start;
  }
```

- [ ] **Step 6: Run focused tests**

Run:

```powershell
dotnet test WeightTracker.sln --filter "Dashboard_WithNoGoal_RendersGoalPanelAndSetAction|Dashboard_WithGoal_RendersGoalPanelAndEditAction|SaveGoal_WithSelectedDisplayUnit_PersistsConvertedGoal|SaveGoal_WithInvalidGoal_ReturnsValidationAndReopensGoalDialog|ClearGoal_RemovesGoalAndPreservesOtherSettings"
```

Expected: PASS.

- [ ] **Step 7: Commit styling changes**

```powershell
git add src/WeightTracker.Web/wwwroot/css/site.css
git commit -m "style: add dashboard goal panel"
```

---

### Task 5: Full Verification

**Files:**
- Verify: all modified source, style, and test files.

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
git diff origin/development...HEAD -- tests/WeightTracker.Tests/DashboardPageTests.cs src/WeightTracker.Web/Pages/Index.cshtml.cs src/WeightTracker.Web/Pages/Index.cshtml src/WeightTracker.Web/wwwroot/css/site.css docs/ROADMAP.md docs/superpowers/specs/2026-06-27-dashboard-goal-feature-design.md docs/superpowers/plans/2026-06-27-dashboard-goal-feature.md
```

Expected: only the dashboard goal feature tests, dashboard model, markup, script, CSS, roadmap cleanup, design spec, and implementation plan appear. No Settings page, no new dependencies, no AJAX endpoints, no client-side routing.

- [ ] **Step 3: Confirm working tree status**

Run:

```powershell
git status --short --branch
```

Expected: clean working tree on the implementation branch after the task commits.

---

## Plan Self-Review

- Spec coverage: covers dashboard Goal panel below latest weight, trophy modal, set/update/clear behavior, display-unit conversion, validation reopening, no separate settings page, no new dependencies, and preserved dashboard flow.
- Placeholder scan: no unresolved placeholders or vague implementation steps.
- Type consistency: uses existing `IndexModel`, `SettingsService.UpdateAsync`, `WeightConversionService`, `MetricsSummary.GoalWeightKg`, `FormatWeight`, `FormatSignedWeight`, and existing test app patterns consistently.
- Scope check: does not add unit, week start, time zone, or theme editing; those remain in the roadmap Settings Page slice.