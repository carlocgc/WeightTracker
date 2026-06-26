# Weight Tracker Design

## Goal

Build a Dockerized, mobile-first web app for manually recording one body weight entry per day, tracking short-term and long-term progress, and making weekly average comparisons prominent.

## Product Scope

The first version is a single-user app intended to run on one machine or home server through Docker Compose. It should be designed so authentication and multiple users can be added later without rewriting the core tracking, metrics, and data access logic.

The app opens directly to the daily weight entry experience. The main page must not be a marketing page or generic dashboard that hides the primary action. The first visible interaction should be entering or updating today's weight.

Version 1 includes:

- Manual weight entry for today's date.
- One entry per local calendar day.
- Re-entering today's weight updates the existing entry.
- Dark mode by default.
- Theme toggle saved in settings.
- General trend chart on the main page.
- Weekly average comparison, matching the user's current Excel-based strategy.
- Historical graphs and metrics.
- Optional goal weight shown as a chart reference line.
- Docker Compose deployment with persistent local storage.

## Card-Entry Amendments (2026-06-26)

- The dashboard is a descending, scrollable date-card feed. Today's card is first on launch, and a user can edit an earlier card without entering a date.
- Each card's date is derived from the saved application time zone. The entry field accepts a decimal weight only in the configured display unit.
- The field requests a decimal mobile keypad and filters input to digits plus one decimal separator; server-side decimal and positive-value validation is authoritative.
- A confirmed, antiforgery-protected action can delete a past card. Today and future cards cannot be deleted.

Deferred features:

- CSV or Excel import.
- Authentication and multiple users.
- Multiple weigh-ins per day.
- Edit audit history.
- Smart scale, wearable, or external service integrations.
- Unraid Community App template, though deployment should be shaped to make one practical later.

## Technology

Use:

- ASP.NET Core Razor Pages for the web app.
- C# as the primary application language.
- SQLite for persistence.
- Entity Framework Core for data access and migrations.
- Chart.js for interactive charts.
- Docker Compose for local and home-server deployment.
- A small custom responsive stylesheet.

Razor Pages is preferred over Blazor Server or a separate JavaScript frontend because it keeps the first version small, understandable, and operationally simple for a homelab app. Chart.js provides enough client-side interactivity for graphs without turning the app into a separate frontend project. A custom stylesheet avoids an extra UI dependency and keeps the visual system tailored to a compact mobile-first tracking app.

## Architecture

The app should use a conventional ASP.NET Core structure with clear service boundaries:

- Razor pages handle routing, page models, form binding, and page-specific view data.
- Entity Framework Core handles persistence.
- Application services contain behavior that should be testable without rendering pages.
- Metrics logic lives outside page models so it can be unit tested and reused by dashboard and history pages.

Core modules:

- `WeightEntry`: persisted daily weight record.
- `AppSettings`: persisted single-user settings.
- `WeightEntryService`: creates, updates, and queries daily entries.
- `MetricsService`: calculates weekly averages, deltas, moving averages, range summaries, and goal progress.
- `SettingsService`: reads and updates display unit, theme, timezone, week start day, and optional goal weight.

The first version can run without authentication, but services should avoid assuming global static user state. When authentication is added later, service methods can accept a user identifier or scoped user context and filter data accordingly.

## Pages

### Dashboard

Route: `/`

Purpose: fast daily entry and immediate progress summary.

Mobile layout:

- Dark mode by default.
- Today's date.
- Today's weight input.
- Save or update button.
- Clear status if today's entry already exists.
- Latest recorded weight.
- Current week average.
- Previous week average.
- Week-over-week delta.
- Recent trend chart with daily entries and an average overlay.
- Optional goal line if goal weight is configured.
- Links or compact navigation to History and Settings.

Desktop layout:

- Same information as mobile.
- Wider chart and metric layout.
- Entry remains visually primary.

### History

Route: `/history`

Purpose: inspect historical data and trends.

Include:

- Date range selection.
- Daily weight chart.
- Weekly average chart.
- Moving average overlay.
- Optional goal line.
- Historical table of entries.
- Summary metrics for the selected range.

### Settings

Route: `/settings`

Purpose: configure display and analysis behavior.

Settings:

- Display unit, default `kg`.
- Optional goal weight.
- Week start day.
- Timezone.
- Theme preference: `dark`, `light`, or `system`.

The default theme is `dark`.

## Data Model

### WeightEntries

Fields:

- `Id`: primary key.
- `EntryDate`: local calendar date for the measurement.
- `WeightKg`: decimal value stored in kilograms.
- `Note`: optional short text note.
- `CreatedAtUtc`: creation timestamp.
- `UpdatedAtUtc`: last update timestamp.

Constraints:

- `EntryDate` must be unique in version 1.
- `WeightKg` must be a positive decimal value.

Behavior:

- Saving a date with no existing row inserts a new entry.
- Saving a date with an existing row updates `WeightKg`, `Note`, and `UpdatedAtUtc`.

### AppSettings

Fields:

- `Id`: primary key.
- `DisplayUnit`: `kg` or `lb`.
- `GoalWeightKg`: nullable decimal.
- `WeekStartsOn`: day of week used for weekly grouping.
- `TimeZoneId`: local timezone identifier for calendar-date behavior.
- `Theme`: `dark`, `light`, or `system`.

Store all weights internally in kilograms. Display conversion happens at the boundary where user input is parsed or output is formatted.

## Metrics

Metrics should make weekly averages prominent because the user's current tracking strategy compares the current weekly average against previous weekly averages.

Dashboard metrics:

- Latest recorded weight.
- Current week average.
- Previous week average.
- Week-over-week delta.
- 7-day moving average.
- 30-day change.
- 90-day change.
- Highest and lowest weight in the selected recent range.
- Goal progress if a goal weight is configured.

History metrics:

- Daily entry trend.
- Weekly average trend.
- Moving average trend.
- Range high and low.
- Range start and end change.
- Goal line overlay when configured.

Weekly averages:

- Group entries by local calendar week using `WeekStartsOn`.
- Average only days with recorded entries.
- Current week average is based on entries recorded so far in the current week.
- Previous week average is based on the immediately preceding calendar week.
- Week-over-week delta equals current week average minus previous week average.

Moving averages:

- A 7-day moving average should use recorded entries within the trailing 7-day window.
- Missing days should not be treated as zero.

## UX Principles

The app should feel like a tool, not a landing page.

Design priorities:

- Mobile-first.
- Entry-first.
- Dark by default.
- Clear numbers.
- Readable charts.
- Minimal friction for daily use.
- No unnecessary navigation before logging today's weight.

The dashboard should answer two questions quickly:

1. Did I log today's weight?
2. What direction is my trend moving?

## Docker And Deployment

The first deployment target is Docker Compose on one machine or home server.

Container expectations:

- One app container.
- SQLite database stored in a mounted data directory.
- Internal database path `/app/data/weighttracker.db`.
- Host volume maps persistent storage to `/app/data`.
- Configurable external port.
- Configurable timezone through environment variable and persisted app setting.

The Compose setup should be simple enough to translate into an Unraid Community App template. Unraid-facing values should include:

- App data path.
- Web port.
- Timezone.
- Optional base URL for reverse-proxy deployments.

## Authentication Extension Path

Version 1 has no login screen.

To keep authentication practical after version 1:

- Avoid hard-coding assumptions that only one data owner can ever exist.
- Keep data access behind services.
- Keep settings access behind a service.
- Keep metrics calculations independent from Razor page models.
- Prefer method signatures that can accept a user scope without changing metric formulas.

When authentication is added, ASP.NET Core Identity is the likely path. At that point, weight entries and settings can be associated with a user id.

## Testing Strategy

Unit tests should cover:

- One-entry-per-day upsert behavior.
- Weight validation.
- Unit conversion boundaries.
- Weekly average grouping.
- Current week versus previous week delta.
- 7-day moving average calculations.
- 30-day and 90-day change calculations.
- Settings persistence rules.
- Theme default and updates.

Integration tests should cover:

- Dashboard loads with no entries.
- Dashboard saves today's weight.
- Saving today's weight again updates the existing entry.
- History page renders with seeded entries.
- Settings page updates persisted preferences.

Build verification should include:

- .NET test run.
- Docker image build.
- Docker Compose configuration validation.

## Success Criteria

The version 1 app is successful when:

- It can be started with Docker Compose.
- Data survives container restarts through a mounted volume.
- The main page opens directly to today's entry field.
- The app defaults to dark mode.
- Theme preference persists.
- Today's weight can be inserted and updated.
- Weekly average comparison is visible on the dashboard.
- Historical graphs show daily weights and aggregate trend lines.
- Optional goal weight appears as a chart reference line.
- Core metrics are covered by tests.

