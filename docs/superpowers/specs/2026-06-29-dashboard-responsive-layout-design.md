# Dashboard Responsive Layout Design

## Context

The dashboard is currently optimized for a phone-width experience. The outer app container is capped at 430px, so desktop users see the same narrow stack centered on a wide page. That preserves the mobile experience, but it wastes desktop space and limits chart readability.

The app should keep its mobile-first dashboard while using wider screens more effectively.

## Goals

- Keep the mobile dashboard as a single scrollable stack in the current order.
- Allow minor mobile polish where it supports the responsive layout or fixes obvious spacing issues.
- Remove the phone-width cap on tablet and desktop.
- Make desktop feel like a full-width dashboard workspace.
- Make the Trend chart the primary desktop panel.
- Keep daily entry, goal editing, trend range selection, history, insights, and data management behavior unchanged.
- Avoid duplicating markup for separate mobile and desktop versions.

## Non-Goals

- Redesigning the product visual language.
- Adding a new charting library.
- Changing dashboard data loading or summary calculations.
- Changing CSV import/export or delete-all behavior.
- Adding new JavaScript interactions unless Chart.js needs a resize fix after layout changes.
- Forcing the full dashboard to fit above the fold on desktop.

## User Experience

Mobile remains the baseline layout. The dashboard order stays:

- Header.
- Latest weight.
- Goal.
- Metric strip.
- Trend.
- Recent history.
- Insights.
- Data.

At narrow widths, the page keeps the existing scrollable card stack. The current `max-width: 430px` phone layout can remain for phones, and the `max-width: 420px` full-bleed treatment can continue to remove the outer frame on very small screens.

Tablet widths, starting at 768px, should use more horizontal space without forcing a cramped desktop grid. The app shell should widen, the dashboard should no longer be capped at 430px, and internal grids such as metrics, insights, and data actions can breathe. The tablet layout should remain mostly stacked.

Desktop widths, starting at 1024px, should use a multi-column dashboard layout. The top desktop area should place a compact summary column beside a larger Trend panel:

- Summary column: header, latest weight, goal, and metric strip.
- Primary area: Trend chart with range selector.

Supporting panels should sit below or alongside the primary area depending on available width:

- Recent history.
- Insights.
- Data.

Normal page scrolling is acceptable. The desktop first view should prioritize the summary column and larger Trend chart, with supporting panels available below rather than squeezed into a fixed-height viewport.

## Architecture

Use light semantic grouping in `src/WeightTracker.Web/Pages/Index.cshtml` around the existing sections. Add these groups:

- `dashboard-summary` for the header, latest weight, goal, and metrics.
- `dashboard-primary` for the Trend panel.
- `dashboard-supporting` for Recent history, Insights, and Data.

The groups should not duplicate content. The existing section markup and accessibility labels should remain intact unless a small wrapper requires a neutral `div`.

Most behavior should live in `src/WeightTracker.Web/wwwroot/css/site.css`. The base CSS remains mobile-first. Responsive media queries should layer wider layouts on top:

- `@media (min-width: 768px)`: widen `.app-shell`, remove the narrow dashboard cap, and allow wider stacked/tablet layout.
- `@media (min-width: 1024px)`: turn `.weight-app` into a multi-column grid, with summary and Trend in the top row.
- `@media (min-width: 1280px)`: cap dashboard content at 1240px so ultra-wide screens do not create awkward line lengths or overly stretched controls.

Chart.js remains unchanged. At desktop widths, `.trend-chart-frame` should increase from the mobile height to at least 300px so the chart benefits from the wider layout.

## Error Handling

No data or form error handling should change.

Validation summaries, status messages, dialogs, CSV import errors, delete confirmation, and goal validation should continue to render through the existing Razor Page flow.

The main layout risk is overflow. Responsive CSS should ensure:

- Buttons and range selector labels do not overlap.
- Metric and insight values can wrap where needed.
- Dialog sizing remains viewport-safe.
- The chart canvas remains visible and responsive.

## Testing

Update dashboard page tests only where the rendered HTML structure changes. Good assertions include:

- The new layout grouping classes render.
- Existing dashboard sections still render once.
- Existing trend chart, history, insights, and data controls remain present.

Avoid brittle server-side tests for exact CSS grid placement.

Verification should include:

```powershell
dotnet test WeightTracker.sln
```

Manual browser verification should check:

- Mobile width keeps the scrollable dashboard and section order.
- Tablet width uses more page width without cramped columns.
- Desktop width uses a full-width multi-column layout.
- The Trend chart resizes correctly and remains nonblank.
- Dialogs still open and fit the viewport.
