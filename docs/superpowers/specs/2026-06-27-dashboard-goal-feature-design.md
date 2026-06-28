# Dashboard Goal Feature Design

## Purpose

Add goal management as a focused dashboard feature, not as part of a broad settings page. Users should be able to see whether they have a target weight, understand the current gap from their latest weight, and set, update, or clear the goal without leaving the dashboard.

## Decisions

- Keep `/` as the single dashboard surface for this slice.
- Do not add a separate Settings page for goal management.
- Add a dedicated Goal panel directly below the latest weight hero.
- Use a small trophy icon button in the Goal panel to open a modal.
- Allow users to set, update, and clear the optional goal.
- Keep display unit behavior consistent with existing weight entry input: users enter the goal in their current display unit, while storage remains kilograms.
- Leave display unit, week start, time zone, and theme preferences for a later Settings Page slice.
- Do not add new dependencies, AJAX endpoints, client-side routing, or a full page navigation flow.

## User Experience

The top dashboard remains entry-first. The first section still shows the app header and latest weight hero with the Add / Update action. Immediately below that hero, the dashboard shows a compact Goal panel.

When a goal exists, the panel shows:

- The Goal label.
- The formatted goal weight in the active display unit.
- A short progress detail using the existing goal-distance calculation when latest weight data exists.
- A trophy icon button with an accessible label such as `Edit goal`.

When no goal exists, the panel shows:

- The Goal label.
- A clear empty state such as `No goal set`.
- Supporting text that prompts the user to add a target.
- The same trophy icon button with an accessible label such as `Set goal`.

The trophy button opens a modal. The modal contains one goal-weight input labeled with the current display unit. It has Save and Cancel actions. When a goal exists, it also shows a secondary Clear goal action.

## Data Flow

`IndexModel` remains the page owner. It already loads settings and builds dashboard summary data using `SettingsService`, `MetricsService`, and display-unit formatting helpers. This slice should extend that flow instead of introducing a new page model.

The model should expose enough dashboard state to render the Goal panel and modal:

- Whether a goal is currently set.
- The formatted goal weight.
- The formatted current goal gap when latest weight exists.
- The goal input value converted from stored kilograms into the selected display unit.

Saving a goal posts back to the dashboard through a dedicated handler. The handler validates the input, converts from the current display unit to kilograms, updates the singleton settings record, and redirects back to `/` on success.

Clearing a goal posts to a dedicated clear-goal handler. It sets `GoalWeightKg` to null while preserving the existing display unit, week start, time zone, and theme settings.

## Validation And Errors

Goal input is optional only for the Clear goal action. Saving requires a positive numeric value. Invalid values should return HTTP 200 with the dashboard re-rendered, validation errors visible, and the goal modal reopened.

The dashboard should preserve existing entry validation behavior. Goal validation should not interfere with the Add / Update weight dialog.

If the current settings record contains no goal, Clear goal should be harmless and leave the goal unset.

## UI And Accessibility

The Goal panel should reuse the existing compact dark dashboard visual language: 8px radius, restrained border, raised surface, dense spacing, and mobile-first layout.

The trophy control should be an icon button with an accessible text label. The icon can be rendered as text or inline markup without adding an icon package. It should not rely on color alone to communicate state.

The modal should follow the existing `<dialog>` pattern used by the entry dialog. It should support close, cancel, save, validation reopening, and focus on the goal input when opened.

## Testing

Update dashboard page integration tests to cover:

- Dashboard renders a Goal panel below the latest weight hero.
- No-goal state renders a clear empty state and a trophy action.
- Existing goal state renders the formatted goal and goal gap in the selected display unit.
- Posting a valid goal in the selected display unit persists the converted kilogram value.
- Posting invalid goal input returns validation and reopens the goal modal.
- Clearing an existing goal removes it while preserving the other settings values.
- Existing entry save, delete, chart, and dashboard insight tests continue to pass.

Verification command:

```powershell
dotnet test WeightTracker.sln
```

## Out Of Scope

- Separate Settings page.
- Display unit editing.
- Week start editing.
- Time zone editing.
- Theme editing.
- Goal history, milestones, streaks, or notifications.
- AJAX save flows.
- New third-party dependencies.
- Authentication or multi-user goal ownership.
