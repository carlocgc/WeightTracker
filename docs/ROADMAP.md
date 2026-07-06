# WeightTracker Roadmap

This roadmap tracks future product and delivery work only. Completed feature behavior belongs in `README.md`, tests, and source code; completed agent implementation plans should not be retained here.

## Development Order

### 1. Database Schema Management

Decide whether production startup should continue using `EnsureCreatedAsync` or move to EF Core migrations.

- Prefer migrations before relying on long-lived user data.
- Add initial migration files if choosing migrations.
- Make startup initialization idempotent.
- Keep test database setup simple and isolated.

### 2. Settings Page

Add a settings page so users can change non-goal preferences from the UI.

- Display unit: `kg` or `lb`.
- Week start day.
- Application time zone.
- Theme preference.
- Validate inputs server-side and preserve current settings on invalid submissions.

### 3. Dashboard Accessibility

Improve accessibility and keyboard behavior for existing dashboard dialogs and controls.

- Verify focus movement when opening and closing entry, goal, import, and delete dialogs.
- Ensure validation errors are announced and associated with the relevant fields.
- Confirm chart-adjacent summaries provide enough non-visual information.

### 4. Visual Verification

Add richer visual checks for mobile and desktop layouts.

- Cover compact mobile, tablet, desktop, and wide desktop dashboard layouts.
- Include chart rendering and dialog states in the checks.
- Keep the checks deterministic enough for CI or documented local verification.

### 5. Deployment Documentation

Add deployment notes for Unraid or other home-server targets.

- Document volume placement for persistent SQLite data.
- Describe safe network exposure assumptions and reverse-proxy expectations.
- Keep Docker Hub and local Compose workflows aligned with `README.md`.

### 6. Authentication

Consider authentication only after the single-user local deployment path is stable.

- Decide whether authentication belongs inside the app or should stay delegated to external access-controlled infrastructure.
- Preserve the current warning against direct internet exposure until a security model exists.
