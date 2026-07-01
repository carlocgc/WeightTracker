# Dashboard Motivational Insights Design

## Purpose

The dashboard should shift its deeper insight area from raw stats toward motivational progress. The user should be able to answer three questions quickly:

- Am I moving toward my goal?
- At this pace, when might I reach it?
- What is my best recent goal-directed progress?

This design keeps the app as a single Razor-rendered dashboard at `/` and splits the work into two focused roadmap slices so metric semantics and charting can be implemented and reviewed independently.

## Decisions

- Keep the work dashboard-only for now.
- Keep the existing Razor Pages architecture.
- Do not add a new page, route, modal, dependency, or client-side framework.
- Treat "good direction" as moving toward the configured goal, not always losing weight.
- Suppress overconfident estimates when the data is sparse, flat, or moving away from goal.
- Show personal records only in the direction of the configured goal.
- Split chart work into a second roadmap slice.
- Fix the empty red validation summary strip as part of the first slice.

## Roadmap Slices

### Slice 1: Motivational Dashboard Insights

This slice should improve the existing dashboard insight area without adding new charts.

- Hide the validation summary container unless there are validation errors.
- Rename the existing Insights panel to Progress insights.
- Add small goal-aware direction indicators to deltas and insight cards.
- Add a Goal forecast card.
- Add goal-direction personal records.
- Improve empty states and fallback labels.

### Slice 2: Weekly Delta Visuals

This slice should add chart-focused trend visuals after Slice 1 is stable.

- Add a compact calendar-week delta bar chart.
- Use the configured week-start setting for week buckets.
- Draw bars around a zero line, similar to the supplied reference image.
- Color bars by whether they move toward or away from the configured goal.
- Keep the chart below or beside Progress insights, not in the first hero row.

## Goal Direction

The metrics layer should classify goal direction from the latest weight and configured goal:

- `Loss`: latest weight is above the goal; downward movement is toward goal.
- `Gain`: latest weight is below the goal; upward movement is toward goal.
- `Maintenance`: latest weight is effectively at goal; flat movement is acceptable.
- `None`: no goal is configured or no latest weight exists.

Directional status for a metric should be:

- `TowardGoal`: the value closes the goal gap.
- `AwayFromGoal`: the value widens the goal gap.
- `Neutral`: the value is flat, goal maintenance is satisfied, or there is no meaningful movement.
- `Unknown`: there is not enough data to judge.

The UI should not imply that weight loss is always good. If the user sets a gain goal, upward progress should be positive. If no goal is set, deltas may show raw direction but should not be colored as success or failure.

## Goal Forecast

The forecast should estimate time to goal only when the data supports it.

Use a blended pace:

1. Prefer a valid 30-day pace.
2. Fall back to a valid 90-day pace.
3. Fall back to a valid all-time pace.

A pace is valid only when:

- There are enough entries in the selected window to compare a baseline and latest value.
- The computed change moves toward the configured goal.
- The daily pace is large enough to avoid a meaningless projection.
- A latest weight and goal weight both exist.

The forecast should expose a concise display state such as:

- Estimated target month or date range.
- Moving away from goal.
- Pace too flat to project.
- Need more recent data.
- Set a goal to unlock forecast.

The UI should frame this as an estimate, not a promise.

## Personal Records

Records should be goal-direction only.

For a loss goal, records show the largest loss over each window. For a gain goal, records show the largest gain over each window. For no goal or a maintenance goal, the records section should be hidden or replaced with a neutral empty state.

Record windows:

- 7 days.
- 30 days.
- 90 days.
- All-time.

Each record should include:

- Label, such as Best 30-day progress.
- Signed value in the active display unit.
- Date range when available.
- Neutral fallback when there is not enough data.

Daily records are intentionally out of scope because daily body-weight movement is too noisy for motivational records.

## User Experience

The first viewport should remain compact and entry-first:

- App header.
- Latest weight hero.
- Goal panel.
- Summary metric strip.
- Trend chart.
- Recent history.
- Progress insights.
- Data management.

The Progress insights panel should have clearer hierarchy:

- A Goal forecast card near the top.
- Existing latest/high/low/change metrics with small directional indicators where useful.
- Goal-direction personal records beneath the main metrics.

Direction indicators should be small icon-like arrows beside values, not large arrows that dominate the card. Colors should be goal-aware:

- Green for moving toward goal.
- Red for moving away from goal.
- Amber or muted neutral for flat, unknown, maintenance, or no-goal states.

When no goal is set, the dashboard should avoid filling the page with dashes. It should use concise copy such as "Set a goal to unlock forecast" or omit goal-dependent sections.

## Data Flow

`IndexModel.LoadAsync` remains the page owner for dashboard data loading.

`MetricsService` should own the new calculations and expose a richer dashboard insight model. The Razor page should format and render the model rather than computing metric semantics in the view.

The new model can sit beside the existing `MetricsSummary` at first. If the summary grows too broad, later work can split raw summary values from motivational insight values.

No new persistence is needed. All calculations use existing weight entries, app settings, today, and display unit conversion.

## Testing

Slice 1 should add or update tests for:

- Goal direction classification.
- Directional statuses for goal-aware deltas.
- Forecast projection when 30-day pace is valid.
- Forecast fallback to 90-day or all-time pace.
- Forecast suppression when data is insufficient, flat, or moving away from goal.
- Goal-direction records for loss and gain goals.
- No records or neutral fallback for no-goal and maintenance states.
- Dashboard rendering of Progress insights, Goal forecast, and Personal records.
- Validation summary not rendering as an empty red strip when there are no errors.

Slice 2 should add or update tests for:

- Calendar-week delta grouping using the configured week-start setting.
- Goal-aware bar status.
- Weekly delta chart data rendering on the dashboard.

Verification command:

```powershell
dotnet test WeightTracker.sln
```

## Error Handling And Empty States

Insufficient data is not an error. The dashboard should show quiet, neutral copy instead of warnings.

Examples:

- "Need more recent data."
- "Pace too flat to project."
- "Set a goal to unlock forecast."
- "No goal-direction record yet."

The app should continue returning HTTP 200 for empty or sparse data sets.

## Later Candidates

These are useful, but should not be included in the two dashboard slices:

- Settings page for display unit, week start, time zone, and theme.
- Reminder or habit-support features.
- Notes attached to weight entries.
- Data quality flags for unusually large jumps.
- Streak or consistency visualization.
- Accessibility polish around dialogs and keyboard flows.

## Out Of Scope

- New History page.
- Full history table.
- New dashboard modals.
- New settings UI.
- Notifications or reminders.
- Entry notes.
- Authentication.
- New third-party dependencies.
- Pixel-perfect chart screenshot testing in Slice 1.
