# Story 8.6 Follow-up: Task and Dashboard UX Polish (2026-05-03)

Status: done

## Scope

This artifact captures the follow-up UI/UX work completed after Story 8.6, focused on task list filtering/visual behavior and dashboard momentum section refinements.

## What Changed

### Task list UX

- Added priority-dependent task card styling so visual emphasis reflects `low`, `medium`, and `high` priority.
- Added two planning filters to task list:
  - `Title`
  - `Priority`
- Reordered planning filters to requested sequence:
  - Title
  - Priority
  - Difficulty
  - Energy
  - Context
- Added tab-switch animation behavior when toggling task list states/tabs.
- Kept reduced-motion accessibility fallback for animation.

### Task list/backend filter support

- Extended frontend task filter contracts and query serialization to include title/priority.
- Extended tasks API query parsing/validation and repository filtering for title/priority.
- Added integration test coverage for combined title+priority filtering.

### Dashboard momentum UX and navigation

- Set momentum default window to 14 days.
- Hid weekly granularity option in dashboard momentum controls.
- Added border and visual container treatment for momentum summary panel.
- Added momentum summary heading structure and refined copy placement:
  - External section title retained.
  - In-box title and subtitle added and aligned with controls.
  - Final layout places "Momentum Overview" + descriptive text in the same row as the window filter.
- Improved momentum route behavior:
  - Implemented route-aware scrolling to momentum section when navigating via `/momentum`.
  - Added sticky-header offset and retry timing for post-render stability.
- Removed redundant top-level `Momentum` tab from app primary navigation (momentum lives on dashboard).

### Task create/edit defaults

- Standardized effort default/fallback values to `50` in create and edit flows.

## Files Updated

- `bmad/task-tracker-web/src/app/features/dashboard/dashboard.component.ts`
- `bmad/task-tracker-web/src/app/features/dashboard/dashboard.component.spec.ts`
- `bmad/task-tracker-web/src/app/features/tasks/create-task.component.ts`
- `bmad/task-tracker-web/src/app/features/tasks/task-list.component.ts`
- `bmad/task-tracker-web/src/app/features/tasks/task-list.component.html`
- `bmad/task-tracker-web/src/app/features/tasks/task-list.component.scss`
- `bmad/task-tracker-web/src/app/features/tasks/task-list.component.spec.ts`
- `bmad/task-tracker-web/src/app/app.ts`
- `bmad/task-tracker-web/src/app/app.spec.ts`
- `bmad/task-tracker-web/src/app/shared/models/task.models.ts`
- `bmad/task-tracker-web/src/app/shared/services/task.service.ts`
- `bmad/task-tracker-web/src/app/shared/services/task.service.spec.ts`
- `bmad/task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs`
- `bmad/task-tracker-api/TaskTracker.Api/Features/Tasks/Contracts/TaskContracts.cs`
- `bmad/task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs`
- `bmad/task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs`
- `bmad/task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs`

## Validation

- Frontend tests were run repeatedly after UI updates:
  - `npx ng test --watch=false --browsers=ChromeHeadless --no-progress`
  - Latest result during follow-up polish: `TOTAL: 145 SUCCESS`.
- Backend tests for task filter behavior were run during the broader change set and passed earlier in session.

## Notes

- This follow-up artifact documents incremental UX/polish changes layered on top of the existing Story 8.6 baseline.
- Route alias cleanup for `/momentum` was discussed as optional and not included in this follow-up unless requested separately.
