# CSV Data Management Design

## Context

WeightTracker stores one weight entry per local calendar date. Entries are stored internally in kilograms with three decimal places, while display unit, goal, week start, time zone, and theme live in app settings.

This feature adds a small data-management surface for backups, migration, and clearing weight history. It intentionally handles only weight-entry data. Settings remain outside the CSV contract and are not affected by import, export, or delete-all.

## Goals

- Export all weight entries to a portable CSV file.
- Import weight entries from a CSV file for backup restore or migration.
- Replace existing entries by date during import.
- Reject invalid imports before any data is written.
- Delete all weight entries only after a two-tier destructive confirmation.
- Preserve app settings during import and delete-all.

## Non-Goals

- Exporting or importing app settings.
- Treating CSV as a full-fidelity database dump.
- Adding a separate settings or backup page.
- Supporting unit-specific CSV imports in pounds or display-unit values.
- Deleting settings, goals, or preferences.

## CSV Contract

CSV v1 uses these columns, in this order:

```csv
entry_date,weight_kg,note
2026-06-28,82.125,
```

- `entry_date` is required and must be ISO `yyyy-MM-dd`.
- `weight_kg` is required and must be an invariant-culture decimal.
- `weight_kg` must be from `0.1` through `1000`, inclusive.
- `weight_kg` must have no more than three decimal places.
- `note` is optional and must be at most 500 characters.
- Export always writes kilograms, regardless of the current display unit setting.

## User Experience

The existing dashboard gets a compact Data section near the bottom, after the insights content. It contains:

- An Export CSV action.
- A CSV file input and Import action.
- A destructive Delete all action.

Export downloads a file named like `weighttracker-weights-YYYYMMDD.csv`.

Import displays validation errors in the existing validation summary, using row numbers where possible. A successful import redirects back to the dashboard with a short status message such as `Imported 12 entries.`

Delete all uses two tiers:

1. A first warning dialog explains that all weight entries will be permanently deleted.
2. A second confirmation requires the user to type exact `DELETE`.

The server-side delete handler also requires exact `DELETE`, so the client-side confirmation is not the only guard. A successful delete redirects back with a status message such as `Deleted 12 entries.`

## Import Behavior

Import accepts one uploaded `.csv` file. The service parses and validates the entire file before saving anything.

Validation rejects:

- Missing required headers.
- Unknown or malformed rows that cannot be parsed as CSV.
- Invalid dates.
- Future-dated entries relative to the app's current local date.
- Missing, non-numeric, out-of-range, or over-precision weights.
- Notes longer than 500 characters.
- Duplicate `entry_date` values inside the uploaded CSV.

If validation succeeds, import upserts by `entry_date`:

- Existing entries with matching dates are updated with the imported weight and note.
- Missing entries are inserted.
- Existing dates absent from the CSV are left untouched.

The import is all-or-nothing. Invalid files produce no database writes.

## Export Behavior

Export reads all weight entries ordered by date ascending and writes the v1 columns. It escapes CSV values correctly, including notes with commas, quotes, or line breaks. Export does not mutate application state.

## Delete-All Behavior

Delete all removes every `WeightEntry` row and leaves `AppSettings` untouched. It reports how many entries were deleted. It does not reset goals or other preferences.

## Architecture

Add `WeightDataService` under `src/WeightTracker.Web/Services` and register it with dependency injection.

The service owns:

- CSV export formatting.
- CSV import parsing and validation.
- Import upsert persistence.
- Delete-all persistence.

The dashboard page model owns HTTP concerns only:

- `OnGetExportCsvAsync` returns the CSV file result.
- `OnPostImportCsvAsync` accepts the uploaded file, calls the service, maps validation errors into `ModelState`, and redirects on success.
- `OnPostDeleteAllWeightsAsync` checks the confirmation value through the service or before calling it, then redirects on success.

This keeps parsing and destructive data operations out of the already large dashboard page model while keeping the UI surface on the dashboard.

## Error Handling

Service-level import validation returns structured errors that include row numbers when the error belongs to a row. Page handlers display those errors through the existing validation summary.

Unexpected persistence failures are allowed to surface through normal ASP.NET error handling. They should not be swallowed as successful imports or deletes.

## Testing

Add focused tests around `WeightDataService` and page handlers where practical:

- Export orders rows by date and writes correct headers.
- Export escapes notes containing commas, quotes, and line breaks.
- Valid import inserts new entries.
- Valid import updates existing entries by date.
- Invalid import rejects without partial writes.
- Future dates, duplicate CSV dates, invalid weights, missing headers, and long notes are rejected.
- Delete all removes weight entries while preserving settings.
- Delete all refuses an incorrect confirmation value.
- Dashboard handlers surface validation/status messages for import and delete-all.

## Documentation

Update durable docs after implementation:

- README should mention CSV export/import as the backup and migration path.
- ROADMAP should move or revise the CSV backup item so it no longer claims settings import/export is planned for this feature.
