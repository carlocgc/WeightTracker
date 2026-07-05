# Weekly Delta Bar Graph Design

## Context

The dashboard currently shows a single `Week delta` metric, computed from the current configured week's average weight compared with the previous configured week's average. The dashboard also renders a Chart.js `Trend` line chart for absolute weight over time.

The next dashboard feature should make week-over-week change visible as a compact bar graph without replacing the existing metric strip or mixing weekly deltas into the absolute-weight trend chart.

## Goals

- Add a dashboard `Weekly change` panel.
- Show a history of week-over-week deltas for the last 12 configured calendar weeks, including the current partial week.
- Compute each delta from weekly average weights, not last recorded weights.
- Use the configured week start setting for week buckets.
- Use goal-aware bar colors when a goal is configured.
- Show sparse or missing data honestly as gaps.
- Reuse Chart.js and the existing Razor Pages dashboard architecture.

## Non-Goals

- Adding a new charting library.
- Adding a new route, endpoint, or AJAX data source.
- Adding a range selector to the weekly chart.
- Replacing the existing `Week delta` metric.
- Changing existing trend chart behavior.
- Changing weight entry, goal, CSV, or settings behavior.

## User Experience

The dashboard gets a new `Weekly change` panel after the `Trend` panel in the mobile order. The panel heading should show `Weekly change` with a compact subtitle such as `Last 12 weeks`.

The chart renders 12 bars ending with the current configured week. Positive deltas extend upward from a zero baseline and negative deltas extend downward. The current partial week is included as the rightmost bucket and should be labeled as `This week` in tooltip context.

Weeks that cannot produce a delta because either the current week or the immediately previous configured week has no entries remain in the output but render as a gap in the bar chart.

Tooltips should show:

- Week range.
- Current weekly average in the active display unit.
- Previous weekly average in the active display unit.
- Signed delta in the active display unit.

No range selector is included in the first version. The existing Trend chart already provides time range controls, and adding another control would make this compact panel heavier than its job.

## Color Semantics

When a goal is set, bar color should classify the delta relative to goal direction:

- Loss goal: negative delta is toward goal, positive delta is away from goal.
- Gain goal: positive delta is toward goal, negative delta is away from goal.
- Maintenance goal: small changes inside the existing maintenance tolerance are neutral; larger changes are away from goal.

When no goal is set, color should describe raw direction without implying good or bad:

- Down.
- Up.
- Flat.

The implementation should reuse the existing `GoalDirection`, `DirectionalStatus`, and maintenance tolerance concepts where practical so the dashboard does not develop conflicting interpretations of progress.

## Data Model

Add a weekly delta model exposed by the metrics layer. A suitable shape is:

- `WeekStart`
- `WeekEnd`
- `CurrentWeekAverageKg`
- `PreviousWeekAverageKg`
- `DeltaKg`
- `IsCurrentWeek`
- `DirectionalStatus`

`DeltaKg`, `CurrentWeekAverageKg`, and `PreviousWeekAverageKg` may be nullable when a bar cannot be computed.

## Data Flow

`IndexModel.LoadAsync` remains the owner of dashboard data loading. It already loads all entries up to `Today`; the weekly delta series should be computed from that loaded entry set using:

- `Today`
- `settings.WeekStartsOn`
- `settings.GoalWeightKg`
- the existing entries up to `Today`

The page model exposes the resulting weekly delta list to `Index.cshtml`. The Razor page serializes it into the existing inline dashboard script alongside the trend data. The browser uses Chart.js to render a single bar chart instance.

No new server endpoint is needed.

## Weekly Bucket Rules

The service should build exactly 12 visible configured week buckets ending with the current week. For each bucket:

- Average all entries whose dates fall within that configured week.
- Cap the current week end date at `today`.
- Compare the bucket average with the immediately previous configured week's average.
- Return a nullable delta when either adjacent week has no entries.
- Preserve the bucket in the output even when the delta is null.

For the first visible week, the immediately previous week may be outside the visible 12-week range. The service should still use it if entries exist, so the first visible bar is not artificially empty when enough data exists.

Future-dated entries should not affect the chart. Dashboard loading already requests entries only through `Today`, and this behavior should remain unchanged.

## Placement And Layout

Mobile order should be:

- Header.
- Latest weight.
- Goal.
- Metric strip.
- Trend.
- Weekly change.
- Recent history.
- Progress insights.
- Data.

Desktop should keep the existing primary top row:

- Summary on the left.
- Trend on the right.

The supporting area should add `Weekly change` alongside `Recent history`, `Progress insights`, and `Data`, arranged responsively without duplicating markup.

Styling should follow the current dashboard panel language:

- 8px border radius.
- Dark raised surface.
- Restrained border.
- Dense but readable spacing.
- Stable chart frame height.

The weekly chart frame can be shorter than Trend, around 180-220px on mobile and slightly taller on wider layouts if the grid gives it space.

## Error Handling And Empty States

With no entries, the page still returns HTTP 200 and renders the `Weekly change` panel with empty or gap-only chart data.

With only one populated week, the chart should render the visible buckets but no computable delta.

With sparse entries, missing adjacent weeks become gaps rather than carrying weights forward or interpolating.

If Chart.js is unavailable, the chart initialization should fail quietly in the same spirit as the existing trend chart setup.

## Testing

Add or update `MetricsService` tests to cover:

- The service builds 12 configured week buckets ending at the current week.
- Deltas use weekly averages rather than last recorded entries.
- The current partial week is included and its end date is capped at `today`.
- Missing adjacent weeks produce null deltas and preserve visible buckets.
- The first visible week can use the previous out-of-range week for its delta.
- Goal-aware directional status classifies toward, away, and neutral cases.

Add or update dashboard page tests to cover:

- The dashboard renders the `Weekly change` panel.
- The weekly chart canvas renders once.
- Serialized weekly delta data includes expected week dates and delta values.
- Existing trend chart, metric strip, history, insights, and data management behavior remain present.

Verification command:

```powershell
dotnet test WeightTracker.sln
```

## Out Of Scope

- Weekly chart range controls.
- A separate analytics page.
- Carry-forward or interpolated weekly values.
- Per-week drilldown.
- Persistence changes.
- New dependencies.
