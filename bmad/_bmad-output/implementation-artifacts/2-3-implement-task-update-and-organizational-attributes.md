# Story 2.3: Implement Task Update and Organizational Attributes

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to edit task details and organization fields,
so that task planning stays current and manageable.

## Acceptance Criteria

1. Given a task owned by the user, when edit request updates title, due date, or category/priority, then changes are persisted and returned.
2. Updates to tasks not owned by requester are denied.

## Tasks / Subtasks

- [x] Add backend update-task contracts and endpoint support (AC: 1, 2)
  - [x] Extend `TaskTracker.Api.Features.Tasks.Contracts` with update request/response DTOs using existing naming and JSON casing conventions.
  - [x] Add update route under `/api/v1/tasks/{taskId}` in `TasksController` using an update verb aligned with current API conventions.
  - [x] Validate task identifier format and request payload at API boundary; return RFC 7807 Problem Details for invalid input.

- [x] Implement ownership-enforced update workflow in repository/application layer (AC: 1, 2)
  - [x] Extend `ITaskRepository` and `TaskRepository` with update behavior scoped to authenticated owner only.
  - [x] Ensure updates only affect permitted mutable fields (`title`, `description`, `dueAtUtc`, `priority`, `category`) and never ownership/system fields.
  - [x] Return normalized task payload including refreshed `updatedAtUtc` for immediate frontend reconciliation.

- [x] Enforce organizational attribute and date validation rules (AC: 1)
  - [x] Validate title length/required constraints and category/priority allowed-value rules consistently with create/list flows.
  - [x] Enforce UTC datetime handling for `dueAtUtc` and reject non-conforming values with deterministic validation errors.
  - [x] Preserve consistent stable app error `code` plus `traceId` in Problem Details responses.

- [x] Build frontend task-edit interaction in tasks feature area (AC: 1)
  - [x] Add edit UI entrypoint from task list with keyboard-accessible controls and clear labels.
  - [x] Add task-edit form behavior for title, due date, category, and priority with inline validation and preserved input on errors.
  - [x] Integrate update call in `TaskService` and refresh local list state without forcing full page reload.

- [x] Preserve auth and ownership boundary behavior (AC: 2)
  - [x] Require authenticated user context on update endpoint.
  - [x] Return unauthorized/forbidden outcomes using consistent auth error contract patterns from prior stories.
  - [x] Add regression coverage proving cross-user task updates are denied.

- [x] Add backend integration tests for happy path, validation, and ownership denial (AC: 1, 2)
  - [x] Test successful update of an owned task persists fields and returns normalized payload.
  - [x] Test invalid payloads/dates/enum values return deterministic Problem Details with `code` and `traceId`.
  - [x] Test update attempts against non-owned tasks return forbidden and do not mutate data.

- [x] Add frontend unit/component tests for edit flow and error handling (AC: 1)
  - [x] Test request mapping and response reconciliation in task service.
  - [x] Test edit form validation behavior and accessible label/control wiring.
  - [x] Test API error presentation and retry path without losing user-entered form values.

## Dev Notes

- Story 2.1 established task schema and create contract; Story 2.2 added list/filter behavior. This story should extend the same feature areas and avoid introducing parallel task models.
- Keep all API routes versioned under `/api/v1` and preserve RFC 7807-style error contracts with stable application `code` and `traceId`.
- Ownership checks remain server-authoritative for every mutation path; caller identity must come only from authenticated context.
- Data platform remains SQL Server via EF Core and should follow existing persistence and migration conventions.
- This story prepares data consistency for Story 2.4 completion toggles and Story 2.5 deletion flows; maintain deterministic mutation semantics.

### API Contracts

Suggested update contract baseline:

```
PUT /api/v1/tasks/{taskId}
Authorization: Bearer <access-token>
Content-Type: application/json

{
  "title": "Plan sprint backlog v2",
  "description": "Finalize priorities after stakeholder sync",
  "dueAtUtc": "2026-04-28T17:00:00Z",
  "priority": "high",
  "category": "planning"
}

HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12",
  "title": "Plan sprint backlog v2",
  "description": "Finalize priorities after stakeholder sync",
  "dueAtUtc": "2026-04-28T17:00:00Z",
  "priority": "high",
  "category": "planning",
  "isCompleted": false,
  "createdAtUtc": "2026-04-25T11:30:12Z",
  "updatedAtUtc": "2026-04-26T09:15:03Z"
}
```

Ownership denial contract example:

```
HTTP/1.1 403 Forbidden
Content-Type: application/problem+json

{
  "type": "https://api.tasktracker.local/problems/forbidden",
  "title": "Forbidden",
  "status": 403,
  "code": "auth.forbidden",
  "traceId": "0HN1FDHJ..."
}
```

### Previous Story Intelligence

- Story 2.2 already introduced task state filtering and deterministic list ordering; update responses should preserve list compatibility and avoid shape drift.
- Story 2.2 established API validation conventions for invalid query state values using Problem Details with stable `code` and `traceId`; mirror this for update payload validation.
- Story 2.1 established ownership-authoritative persistence behavior; update flow must continue to prevent cross-user mutation and payload-based ownership spoofing.
- Existing frontend task feature and `TaskService` are now the contract surface for list/create operations; extend that layer rather than introducing duplicate task APIs.

### Git Intelligence Summary

- Recent repository history currently shows a single baseline commit (`Task Tracker creation`), so story-to-story implementation artifacts are the primary source of conventions.

### Project Structure Notes

- Backend expected touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Contracts/TaskContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs`

- Frontend expected touch points:
  - `task-tracker-web/src/app/features/tasks/`
  - `task-tracker-web/src/app/shared/models/task.models.ts`
  - `task-tracker-web/src/app/shared/services/task.service.ts`

- Architecture target folders in planning docs include deeper layered projects (`TaskTracker.Application`, `TaskTracker.Domain`, `TaskTracker.Infrastructure`), but current implementation remains feature-first in `TaskTracker.Api`; maintain the implemented structure for this story.

### Testing Requirements

- Verify successful owned-task update persists allowed fields and returns normalized payload with refreshed `updatedAtUtc`.
- Verify invalid payloads return RFC 7807 Problem Details with stable `code` and `traceId`.
- Verify unauthorized and forbidden ownership cases follow consistent auth error contracts and do not mutate data.
- Verify frontend edit form behavior preserves user input after validation/API errors and provides accessible field/control semantics.
- Verify mobile and desktop behavior for edit interaction remains usable with keyboard/touch targets.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 2, Story 2.3]
- Functional requirements (`FR10`, `FR13`, `FR27`) and performance/security constraints (`NFR3`, `NFR7`): [Source: _bmad-output/planning-artifacts/epics.md, Functional Requirements and NonFunctional Requirements]
- Product-level task CRUD and ownership requirements: [Source: _bmad-output/planning-artifacts/prd.md, Task Management FRs and Authorization/Access Control]
- API conventions and architectural constraints (`/api/v1`, Problem Details, SQL Server EF Core, ownership checks): [Source: _bmad-output/planning-artifacts/architecture.md, API and Communication Patterns; Authentication and Security; Data Architecture]
- UX guidance for low-friction task flows, accessibility, and responsive behavior: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Core User Experience; Accessibility Considerations]
- Previous implementation baseline: [Source: _bmad-output/implementation-artifacts/2-2-build-task-list-views-for-active-and-completed-items.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Implemented `PUT /api/v1/tasks/{taskId}` with route GUID validation, shared payload validation, and UTC enforcement for `dueAtUtc`.
- Added repository-level ownership-enforced update flow returning deterministic outcomes for updated/forbidden/not-found states.
- Added integration tests covering update success, unauthorized, validation errors (including non-UTC datetime), and cross-user ownership denial.
- Added inline edit workflow in task list with keyboard-accessible entrypoint, form validation, Problem Details field mapping, and in-memory list reconciliation.
- Added frontend tests for `TaskService.updateTask` request mapping and task list edit success/error behavior with input preservation.
- Verification: `dotnet test task-tracker-api/tests/TaskTracker.Api.Tests/TaskTracker.Api.Tests.csproj` (56 passed), `npx ng test --watch=false --browsers=ChromeHeadless` (47 passed).

### File List

- task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Contracts/TaskContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs
- task-tracker-web/src/app/features/tasks/task-list.component.ts
- task-tracker-web/src/app/features/tasks/task-list.component.html
- task-tracker-web/src/app/features/tasks/task-list.component.scss
- task-tracker-web/src/app/features/tasks/task-list.component.spec.ts
- task-tracker-web/src/app/shared/models/task.models.ts
- task-tracker-web/src/app/shared/services/task.service.ts
- task-tracker-web/src/app/shared/services/task.service.spec.ts
- _bmad-output/implementation-artifacts/2-3-implement-task-update-and-organizational-attributes.md
