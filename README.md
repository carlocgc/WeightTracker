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
- Service, persistence, startup, and dashboard tests.

Remaining product and deployment work is tracked in:

- `docs/ROADMAP.md`

## Development

Restore dependencies, build the solution, and run the test suite with:

```powershell
dotnet restore WeightTracker.sln
dotnet build WeightTracker.sln
dotnet test WeightTracker.sln
```

## Branches

- `master`: stable branch.
- `development`: integration branch for feature work.
