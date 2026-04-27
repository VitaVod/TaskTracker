# Story 2.2: Build Task List Views for Active and Completed Items

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to view active and completed tasks distinctly,
so that I can focus on what remains and review what is done.

## Acceptance Criteria

1. Given tasks exist in mixed states, when tasks are requested, then API and UI expose active/completed filters.
2. State labels are clear and accessible.

## Tasks / Subtasks

- [x] Add backend list-query contracts and endpoint support for task state filtering (AC: 1)
  - [x] Extend `TaskTracker.Api.Features.Tasks.Contracts` with list query and response contracts that keep existing naming and JSON casing conventions.
  - [x] Add `GET /api/v1/tasks` in `TasksController` with optional `state` filter (`active`, `completed`, `all`) and deterministic default behavior.
  - [x] Keep response/error shape aligned with existing API conventions and RFC 7807 for invalid filter values.

- [x] Implement repository query for owned task lists partitioned by completion state (AC: 1)
  - [x] Extend `ITaskRepository` and `TaskRepository` with read methods that enforce server-side ownership boundaries.
  - [x] Ensure query ordering is deterministic for rendering stability (for example by completion state + `updatedAtUtc` descending).
  - [x] Preserve SQL Server + EF Core patterns established in story 2.1 and existing persistence mappings.

- [x] Build frontend list view with active/completed filter controls (AC: 1, 2)
  - [x] Add task-list page/component(s) under the existing tasks feature folder and wire route(s) through `app.routes.ts`.
  - [x] Extend `TaskService` and task models to fetch list data with filter parameters.
  - [x] Render active/completed segments with explicit text labels, not color-only state indication.

- [x] Implement accessibility guardrails for state labels and filter controls (AC: 2)
  - [x] Ensure filter controls are keyboard-operable and expose accessible names.
  - [x] Announce filter-result changes in an assistive-technology-friendly way when the active filter changes.
  - [x] Keep focus-visible and responsive behavior aligned with UX standards.

- [x] Add backend integration tests for list filtering and ownership enforcement (AC: 1)
  - [x] Add tests in `TaskTracker.Api.Tests/Integration/TasksControllerTests.cs` covering mixed-state retrieval and filter behavior.
  - [x] Add regression tests ensuring users cannot read another user's tasks.
  - [x] Add invalid-filter test asserting stable Problem Details `code` + `traceId` behavior.

- [x] Add frontend unit/component tests for filtering and accessible labels (AC: 1, 2)
  - [x] Add tests for request parameter mapping and list rendering in task feature components/services.
  - [x] Validate filter toggle behavior and visible label semantics.
  - [x] Validate keyboard interaction and focus behavior for filter controls.

## Dev Notes

- Story 2.1 already established task ownership, task schema, and task create contract. Story 2.2 should build on that foundation by adding read/list capability instead of introducing new task shape variants.
- Ownership must remain server-side enforced: list responses must only include tasks belonging to the authenticated principal.
- Keep all task API endpoints under `/api/v1/tasks` and maintain standardized Problem Details for invalid requests.
- This story is the read/list baseline for later task update/toggle/delete stories in Epic 2; keep contracts extensible for upcoming attributes and mutation flows.

### API Contracts

Suggested list contract baseline:

```
GET /api/v1/tasks?state=active
Authorization: Bearer <access-token>

HTTP/1.1 200 OK
Content-Type: application/json

{
  "items": [
    {
      "id": "7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12",
      "title": "Plan sprint backlog",
      "description": "Draft story priorities for next sprint",
      "dueAtUtc": "2026-04-27T18:00:00Z",
      "priority": "medium",
      "category": "planning",
      "isCompleted": false,
      "createdAtUtc": "2026-04-25T11:30:12Z",
      "updatedAtUtc": "2026-04-25T11:30:12Z"
    }
  ],
  "summary": {
    "activeCount": 3,
    "completedCount": 1
  }
}
```

Invalid filter contract example:

```
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://api.tasktracker.local/problems/validation",
  "title": "Validation failed",
  "status": 400,
  "code": "validation.request.invalid",
  "traceId": "0HN1FDHJ...",
  "errors": {
    "state": ["The state filter must be one of: active, completed, all."]
  }
}
```

### Previous Story Intelligence

- Story 2.1 implemented create-task in `TasksController`, `TaskContracts`, and `TaskRepository`; extend these same feature areas for consistency.
- Existing tests in `TasksControllerTests` already validate authentication, ownership, and Problem Details conventions; mirror this structure for new list/filter tests.
- Frontend task create flow uses `TaskService` and shared task models; list/filter operations should reuse this service/model layer rather than introducing parallel APIs.

### Git Intelligence Summary

- Repository currently has a single baseline commit (`Task Tracker creation`), so there are no additional incremental commit patterns to mine for this story.

### Project Structure Notes

- Backend expected touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Contracts/TaskContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs`

- Frontend expected touch points:
  - `task-tracker-web/src/app/app.routes.ts`
  - `task-tracker-web/src/app/features/tasks/` (new list view component(s) and tests)
  - `task-tracker-web/src/app/shared/models/task.models.ts`
  - `task-tracker-web/src/app/shared/services/task.service.ts`

- Architecture target folders in planning docs include deeper layering (`TaskTracker.Application`, `TaskTracker.Domain`, `TaskTracker.Infrastructure`), while current implementation is feature-first under `TaskTracker.Api`. Continue current implemented structure and avoid introducing half-migrated layers in this story.

### Testing Requirements

- Verify authenticated users can request task lists with `active`, `completed`, and default/all behavior.
- Verify mixed-state datasets are partitioned correctly and counts/labels are consistent.
- Verify users cannot access tasks owned by other users.
- Verify invalid filter values return RFC 7807 Problem Details with stable `code` and `traceId`.
- Verify frontend filter controls are keyboard-accessible and labels communicate state beyond color.
- Verify mobile and desktop rendering preserve filter usability and state clarity.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 2, Story 2.2]
- Task and access functional requirements (`FR9`, `FR14`, `FR27`): [Source: _bmad-output/planning-artifacts/prd.md, Functional Requirements]
- API conventions and ownership boundaries (`/api/v1`, Problem Details, ownership checks): [Source: _bmad-output/planning-artifacts/architecture.md, API and Communication Patterns; Authentication and Security; Implementation Patterns and Consistency Rules]
- UX accessibility and responsive patterns (filter hierarchy, empty/loading guidance, keyboard/screen-reader expectations): [Source: _bmad-output/planning-artifacts/ux-design-specification.md, UX Consistency Patterns; Responsive Design & Accessibility]
- Previous implementation baseline: [Source: _bmad-output/implementation-artifacts/2-1-create-task-domain-and-api-contracts.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Implemented `GET /api/v1/tasks` with optional `state` filtering (`active`, `completed`, `all`) and RFC 7807 validation response for invalid filter values.
- Added ownership-enforced repository list and summary count queries with deterministic ordering (`isCompleted`, `updatedAtUtc`, `createdAtUtc`, `id`).
- Added backend integration tests for default/all behavior, completed filtering, ownership boundaries, and invalid state validation.
- Implemented frontend task-list page with active/completed/all filters, keyboard-operable controls, and live-region announcement updates.
- Extended task models and service with list/filter contracts, wired `/tasks` route, and added service/component tests for filtering and accessibility labels.
- Verification: `dotnet test` (52 passed), `ng build` (success), `npx ng test --watch=false --browsers=ChromeHeadless` (44 passed).

### File List

- task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Contracts/TaskContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs
- task-tracker-web/src/app/app.routes.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.ts
- task-tracker-web/src/app/features/tasks/task-list.component.ts
- task-tracker-web/src/app/features/tasks/task-list.component.html
- task-tracker-web/src/app/features/tasks/task-list.component.scss
- task-tracker-web/src/app/features/tasks/task-list.component.spec.ts
- task-tracker-web/src/app/shared/models/task.models.ts
- task-tracker-web/src/app/shared/services/task.service.ts
- task-tracker-web/src/app/shared/services/task.service.spec.ts
- _bmad-output/implementation-artifacts/2-2-build-task-list-views-for-active-and-completed-items.md
