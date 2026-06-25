# WeightTracker

WeightTracker is a planned Dockerized, mobile-first web app for manually recording daily body weight and analyzing weight trends.

## Planned Direction

- ASP.NET Core Razor Pages.
- SQLite stored in a Docker volume.
- Entity Framework Core for persistence.
- Chart.js for trend and history graphs.
- Dark mode by default with a saved theme toggle.
- One entry per local calendar day.
- Weekly average comparison as a primary metric.
- Docker Compose first, with a path toward an Unraid Community App template.

## Current Status

The app is in the planning stage. The initial design spec is stored at:

- `docs/superpowers/specs/2026-06-25-weight-tracker-design.md`

## Branches

- `master`: stable branch.
- `development`: integration branch for feature work.
