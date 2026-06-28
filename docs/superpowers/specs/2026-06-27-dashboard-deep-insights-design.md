# Dashboard Deep Insights Design

## Purpose

The next roadmap slice changes from a dedicated History page to deeper insight inside the existing dashboard. The app should remain a single Razor-rendered dashboard at `/`, with the current entry-first top section preserved and deeper charts and metrics revealed by scrolling.

## Decisions

- Keep the existing Razor Pages architecture; do not convert to a client-side SPA.
- Keep `/` as the single dashboard surface.
- Do not add a new History page.
- Do not add new modals in this slice.
- Do not make the top dashboard sticky.
- Keep the current top dashboard visually and behaviorally intact.
- Keep the existing recent history card as a quick glance.
- Do not add a full history table; date-by-date review remains available through the Add / Update calendar dialog.
- Load all entries internally for metrics and all-time charting.

## User Experience

The dashboard remains a single vertical page. The first viewport keeps the existing high-priority workflow:

- App header.
- Latest weight hero.
- Add / Update action.
- Three compact summary metrics.
- Compact trend chart.
- Recent history card.
- Existing entry dialog.

Scrolling below that top dashboard reveals two deeper insight sections:

1. A "Long-term trend" panel with a taller all-time chart.
2. An "Insights" panel with focused long-range metrics.

The deeper sections are read-only. Users continue to add, update, delete, and review dated entries through the existing Add / Update calendar dialog.

## Data Flow

`IndexModel.LoadAsync` remains the page owner. It should continue loading settings, local today, the visible calendar month, and recent dashboard data as it does now.

For this slice it should also load all entries up to today so the page can compute deeper insight without another route, service, or AJAX endpoint.

The model should expose:

- `RecentHistory`: unchanged, still the top seven recent entries.
- `Summary`: metrics across the complete loaded entry set.
- `Chart`: existing compact dashboard chart behavior for the top section.
- `LongRangeChart`: all available entries for the deeper long-term trend section.
- `EntryCount`: total loaded entry count.

No new third-party dependencies are needed.

## Metrics

The deeper metrics grid should show:

- Latest weight.
- Range high.
- Range low.
- 30-day change.
- 90-day change.
- Entry count.

Weight values and signed changes must use the selected display unit consistently with the existing dashboard.

When there are no entries:

- Weight metrics render `-`.
- Entry count renders `0`.
- Chart data arrays are empty.
- The page still returns HTTP 200.

## Charts

The existing compact chart remains in the top dashboard and continues to show the current dashboard range.

The new long-term trend chart should use all available entries and include:

- Daily weights.
- 7-day moving average.
- Goal line when configured.

Chart rendering should reuse the current Chart.js pattern already present on the dashboard. The all-time chart should use a separate canvas id and dataset variable names so the compact chart behavior stays isolated.

## Styling

The new sections should follow the existing compact dark dashboard style:

- 8px radius panels.
- Restrained borders and dark surfaces.
- Dense but readable metric cards.
- Mobile-first layout.

The top dashboard should not be visually redesigned as part of this slice. Styling changes should be limited to the new long-term chart and insights sections plus any small reusable class additions needed to keep the layout consistent.

## Testing

Update dashboard page tests to cover:

- The deeper insight sections render on `/`.
- The long-term chart canvas renders separately from the compact trend chart.
- An older entry outside the compact chart window is included in the all-time chart data.
- The focused metrics render expected display-unit values.
- The design does not introduce a full history table.

Existing save, delete, validation, and calendar tests should continue to pass unchanged.

Verification command:

```powershell
dotnet test WeightTracker.sln
```

## Out Of Scope

- New History page.
- Full history table.
- New modals.
- Sticky headers or sticky summary bars.
- Client-side routing.
- AJAX-loaded insight data.
- New dependencies.
- Settings page work.
- CSV import/export.
- Deployment packaging.
