# WeightTracker Roadmap

This roadmap tracks product and delivery work that is still useful after the initial planning docs have been completed. Completed agent implementation plans and specs should not stay in the repo unless they describe durable behavior that is not documented elsewhere.

## Completed Foundation

- ASP.NET Core Razor Pages app scaffold.
- SQLite persistence through Entity Framework Core.
- Settings persistence for display unit, goal weight, week start, time zone, and theme.
- Time-zone-aware local date resolution.
- One weight entry per local calendar date.
- Save, update, read, and delete behavior for date-based entries.
- Trend metrics service and dashboard chart data.
- Mobile-first dashboard with calendar-based entry dialog.
- Dark, compact application styling.
- Automated tests for services, persistence, startup, and dashboard behavior.
- Scrollable dashboard deep insights with long-term trend and focused metrics.

## Near-Term Work

### Dashboard Goal Feature

Add goal management as a focused dashboard feature rather than a general setting.

- Show a compact Goal panel directly below the latest weight hero.
- Use a small trophy icon button to open a goal modal.
- Let users set, update, and clear the optional goal.
- Keep goal input in the active display unit while storing kilograms internally.
- Preserve the existing dashboard flow without adding a new page.

### Settings Page

Add a settings page so users can change non-goal preferences from the UI.

- Display unit: `kg` or `lb`.
- Week start day.
- Application time zone.
- Theme preference.
- Validate inputs server-side and preserve current settings on invalid submissions.

### Deployment Packaging

Add Docker-based deployment support.

- Add `src/WeightTracker.Web/Dockerfile`.
- Add `docker-compose.yml`.
- Persist SQLite data in a mounted volume or host directory.
- Document direct `dotnet run`, test, and Docker Compose workflows in `README.md`.
- Validate with `dotnet test WeightTracker.sln` and `docker compose config`.

### Database Schema Management

Decide whether production startup should continue using `EnsureCreatedAsync` or move to EF Core migrations.

- Prefer migrations before relying on long-lived user data.
- Add initial migration files if choosing migrations.
- Make startup initialization idempotent.
- Keep test database setup simple and isolated.

### CSV Backup, Export, And Import

Add CSV export and import as a practical backup path.

- Export weight entries with date, stored kilogram value, display value, display unit, and timestamps where available.
- Export settings that fit naturally in CSV, including display unit, goal weight, week start, time zone, and theme.
- Include enough metadata to identify the export format version and creation time.
- Import entries by date, updating existing dates or reporting conflicts through a clear rule.
- Import settings when present, while validating units, time zones, themes, and positive goal weights.
- Treat CSV as a portable backup format, not a full-fidelity database dump.

## Later Work

- Improve dashboard accessibility and keyboard behavior around the entry dialog.
- Add richer visual checks for mobile and desktop layouts.
- Add deployment notes for Unraid or other home-server targets.
- Consider authentication only after the single-user local deployment path is stable.
