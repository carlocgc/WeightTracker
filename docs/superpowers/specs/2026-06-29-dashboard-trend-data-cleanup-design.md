# Dashboard Trend And Data Cleanup Design

## Context

The dashboard currently renders two Chart.js line charts:

- `Trend`, using the most recent 180 days of entries.
- `Long-term trend`, using all available entries.

The Data section currently mixes a direct export link, an inline CSV file input, an import submit button, and the delete-all action. The inline file input makes the section heavier than it needs to be and the export/import actions do not present with matching button typography.

## Goals

- Combine the short-term and long-term weight trend graphs into one chart.
- Add a time frame selector for `1 month`, `3 months`, `6 months`, `1 year`, and `All`.
- Default the combined chart to `6 months` to preserve the current Trend behavior.
- Keep Export CSV as a direct download action.
- Move CSV import file selection into an Import modal.
- Keep the Data section visually simple with only `Export CSV`, `Import CSV`, and `Delete all` actions.
- Normalize Export and Import button text placement and font styling.
- Preserve the existing delete-all two-step confirmation flow.

## Non-Goals

- Adding a new charting library.
- Adding server-side chart range navigation or query-string state.
- Changing the CSV export/import contract.
- Changing delete-all confirmation behavior.
- Adding a separate data management page.
- Redesigning the whole dashboard.

## User Experience

The dashboard keeps one `Trend` panel. The panel heading includes a compact segmented range selector with these options:

- `1M`
- `3M`
- `6M`
- `1Y`
- `All`

The `6M` option is selected by default. Selecting another range updates the existing chart in place without a page reload.

The separate `Long-term trend` panel is removed. The `All` range on the combined chart replaces it.

The Data section contains exactly three visible actions:

- `Export CSV`
- `Import CSV`
- `Delete all`

`Export CSV` remains a direct link to the existing export handler. `Import CSV` opens a modal with a CSV file input, an `Import CSV` submit button, and a cancel/close action. `Delete all` continues to open the existing warning dialog followed by the exact `DELETE` confirmation dialog.

## Data Flow

`IndexModel.LoadAsync` should continue loading all entries up to today for summary, insights, and all-time chart data.

The combined chart should use the all-entry chart series already exposed by `LongRangeChart`, renamed or reused as appropriate. The browser filters the serialized daily and moving-average points by selected range:

- `1 month`: entries on or after today minus 1 month.
- `3 months`: entries on or after today minus 3 months.
- `6 months`: entries on or after today minus 6 months.
- `1 year`: entries on or after today minus 1 year.
- `All`: all serialized points.

The goal line remains optional and uses the filtered chart labels when a goal is configured.

## Architecture

Keep the existing Razor Pages architecture and Chart.js usage.

The page model remains responsible for loading dashboard data. No new endpoint is needed because every range can be derived from the all-entry chart payload already rendered with the page.

The dashboard script should hold a single Chart.js instance for the combined trend chart. Range button clicks should replace chart labels and dataset values, then call `chart.update()`.

The Data section should reuse the existing dialog helper functions for the new import dialog. Import still posts to `OnPostImportCsvAsync` with the existing `ImportFile` bind property.

## Error Handling

If CSV import validation fails, errors continue to render through the existing validation summary. The import modal does not need to reopen automatically after a failed post; the validation summary provides the durable error surface on the returned page.

If there is no chart data in a selected range, the chart renders with empty datasets rather than failing. `All` also renders empty datasets when there are no entries.

## Testing

Update dashboard page tests to cover:

- The dashboard renders one trend chart canvas.
- The old `Long-term trend` panel and `longRangeTrendChart` canvas are no longer present.
- The range selector renders `1M`, `3M`, `6M`, `1Y`, and `All`.
- All-time chart data still includes older entries outside the default six-month view.
- The Data section exposes only the three top-level actions: `Export CSV`, `Import CSV`, and `Delete all`.
- Import uses a dialog containing the CSV file input and import submit action.
- Export still returns the same CSV download response.

Verification command:

```powershell
dotnet test WeightTracker.sln
```
