# Daily Entry Service Design

## Goal

Provide a testable application service that persists a weight for a date-card selected by the application, updating the existing entry for that local calendar date rather than creating a duplicate.

## Scope

The entry UI accepts only a positive weight. Each card has an application-generated local calendar date; the user never types or selects a date. The service receives that date internally so the same operation can save today's card or an earlier card, and obtains the display unit from `AppSettings`. It converts kilograms or pounds to kilograms using the existing `WeightConversionService`, rounds the stored value to three decimal places, and persists it through `WeightTrackerDbContext`.

The service also provides read-only lookups for a single date and for an inclusive date range. Both reads return date-ordered, no-tracking entities. It provides deletion for dates before today. No new routes are included in this service task; card rendering belongs to the dashboard task.

## Design

`IClock` exposes `UtcNow` and is implemented by `SystemClock` in production. Tests use `FixedClock`, making creation and update timestamps deterministic without relying on wall-clock time. A scoped `LocalDateProvider` reads the saved `TimeZoneId`, converts the clock's UTC value to today's local date, and is shared by the dashboard and entry service. The dashboard creates a descending date-card feed with today first; an earlier card carries its own fixed date into the save operation.

`WeightEntryService.SaveAsync` accepts the card's internal `EntryDate` and its weight. It rejects a future date, reads the configured display unit, and finds an entry by the date. If absent, it creates one with both UTC timestamps set to the clock time. If present, it preserves `CreatedAtUtc`, replaces the normalized weight, and updates `UpdatedAtUtc`. Notes are not collected in this stage. The existing database uniqueness constraint remains the final guard against duplicate dates.

`DeletePastAsync` removes an existing entry only when its date is before the shared local today. It rejects today and future dates. The dashboard will expose this operation on past date cards through an antiforgery-protected POST form with an explicit confirmation. The entry service and `LocalDateProvider` are scoped; `SystemClock` is a singleton. The design introduces no new packages or migrations.

The dashboard field will use a decimal numeric input: `inputmode="decimal"` requests a numeric keypad on supported mobile devices, while desktop keeps normal keyboard focus. A small client-side filter allows digits and one decimal separator only; server-side decimal model binding and positive-value validation remain authoritative. Native `type="number"` alone is insufficient because some browsers permit non-numeric exponent characters while editing.

## Error Handling

Input validation for positive weights and the configured unit remains centralized in `WeightConversionService`. The local-date provider rejects an invalid configured time zone through the existing settings validation. `SaveAsync` rejects future dates and `DeletePastAsync` rejects today and future dates. Database failures are not translated or swallowed; callers receive the underlying EF Core exception, preserving actionable failure information.

## Verification

Tests will establish the public behavior before the production types exist. They cover local-date conversion around UTC midnight, configured-unit conversion, insert, update-without-duplicate, rejection of future dates, timestamps, single-date retrieval, inclusive ordered range retrieval, and permitted and rejected deletion. Dashboard tests will cover the today-first card feed, numeric input attributes, server rejection of invalid values, editing an earlier card, and deleting a past card. The focused service tests and the full solution test suite must pass.

## Deliberate Non-Goals

This feature does not implement the date-card feed, browser-side numeric filtering, or concurrent conflict resolution beyond the database uniqueness constraint. Those UI concerns belong to later dashboard work.
