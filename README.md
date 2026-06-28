# WeightTracker

WeightTracker is a mobile-first web app for manually recording daily body weight and analyzing weight trends.

## Current Status

The app currently includes:

- ASP.NET Core Razor Pages.
- SQLite persistence through Entity Framework Core.
- A mobile-first dashboard with calendar-based weight entry.
- One entry per local calendar date.
- Time-zone-aware entry dates.
- Saved settings for unit, goal, week start, time zone, and theme.
- Trend metrics and dashboard chart data.
- CSV export/import and guarded delete-all tools for weight-entry data.
- Service, persistence, startup, and dashboard tests.

Remaining product and deployment work is tracked in:

- `docs/ROADMAP.md`

## Backups And Migration

Use the dashboard Data section to export, import, or clear weight-entry data.

CSV export downloads all recorded weights as `entry_date`, `weight_kg`, and `note` columns. Weights are always exported in stored kilograms, independent of the current display unit.

CSV import accepts the same columns, validates the full file before writing anything, and updates existing entries by `entry_date`. Settings such as display unit, goal, week start, time zone, and theme are not imported or exported.

Delete all removes only weight entries. It leaves settings unchanged and requires a two-step confirmation with exact `DELETE`.

## Development

Restore dependencies, build the solution, and run the test suite with:

```powershell
dotnet restore WeightTracker.sln
dotnet build WeightTracker.sln
dotnet test WeightTracker.sln
```

## Docker

Build and run the app locally with Docker Compose:

```powershell
docker compose up --build
```

The app is published locally on:

```text
http://localhost:18080
```

The container listens on internal HTTP port `8080` and stores SQLite data at `/data/weighttracker.db`. ASP.NET Data Protection keys are stored beside the database at `/data/DataProtectionKeys` by default, so antiforgery tokens survive container recreation. The compose file mounts a named volume at `/data` so local app data survives container recreation. Set `DataProtection__KeysPath` to override the key directory.

To run the published Docker Hub image directly:

```powershell
docker run --name weighttracker --rm -p 18080:8080 -v weighttracker-data:/data carlocgc/weighttracker:latest
```

## Releases

Release tags must use `vX.Y.Z` format and point to a commit contained in `master`.

Pushing a matching tag creates a GitHub Release, attaches a self-contained `linux-x64` app zip, publishes `docker.io/carlocgc/weighttracker:vX.Y.Z`, and updates `docker.io/carlocgc/weighttracker:latest`.

The release workflow expects these repository secrets:

```text
DOCKERHUB_USERNAME
DOCKERHUB_TOKEN
```

## Branches

- `master`: stable branch.
- `development`: integration branch for feature work.
