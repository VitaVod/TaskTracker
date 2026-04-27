# Story 2.5: Implement Task Deletion and Safe UX Confirmation

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to delete tasks I no longer need,
so that my task list remains clean and relevant.

## Acceptance Criteria

1. Given an owned task, when delete is confirmed, then task is deleted (or soft-deleted per policy) and removed from active views.
2. Deletion of another user's task is forbidden.

## Tasks / Subtasks

- [x] Add backend delete-task contracts and endpoint support (AC: 1, 2)
  - [x] Add delete route under `/api/v1/tasks/{taskId}` in `TasksController` using the existing API versioning and response conventions.
  - [x] Validate task identifier format and return deterministic RFC 7807 Problem Details for malformed IDs.
  - [x] Return a deterministic success contract for delete operations (for example `204 No Content` or an explicit delete-result payload).

- [x] Implement ownership-enforced deletion workflow in repository layer (AC: 1, 2)
  - [x] Extend `ITaskRepository` and `TaskRepository` with delete behavior scoped to authenticated owner only.
  - [x] Implement deletion policy selected for this product stage (hard delete or soft delete) with clear persistence semantics.
  - [x] Ensure deleted tasks are excluded from standard active/completed list queries and summaries.

- [x] Preserve deterministic state behavior and safe retry outcomes (AC: 1)
  - [x] Define repeated delete behavior (for example idempotent no-op after successful deletion) and keep responses stable.
  - [x] Ensure retries/reconnect paths do not re-surface deleted tasks in active views.
  - [x] Keep updated timestamps and any event/history side effects aligned with current persistence conventions.

- [x] Build frontend safe confirmation UX for task deletion (AC: 1)
  - [x] Add a clear confirmation flow before delete is executed (modal, inline confirm, or equivalent UX pattern).
  - [x] Keep confirmation controls keyboard-accessible with deterministic focus behavior and explicit button labels.
  - [x] Remove deleted tasks from visible lists immediately after server confirmation without full page reload.

- [x] Preserve auth and ownership boundary behavior (AC: 2)
  - [x] Require authenticated user context on delete endpoint.
  - [x] Return unauthorized/forbidden outcomes using existing auth error contracts from prior stories.
  - [x] Add regression coverage proving cross-user task deletion is denied and does not mutate data.

- [x] Add backend integration tests for success, retry semantics, and ownership denial (AC: 1, 2)
  - [x] Test successful owned-task deletion removes task from subsequent list responses.
  - [x] Test repeated delete requests produce deterministic outcomes without inconsistent state.
  - [x] Test malformed task IDs, unauthorized calls, and cross-user delete attempts return expected Problem Details/auth contracts.

- [x] Add frontend unit/component tests for confirmation and list reconciliation (AC: 1)
  - [x] Test delete request mapping and response handling in `TaskService`.
  - [x] Test confirmation UX behavior (open, cancel, confirm) and keyboard interaction paths.
  - [x] Test list state reconciliation and error recovery messaging when deletion fails.

## Dev Notes

- Story 2.1 established create contracts and ownership persistence; Story 2.2 established active/completed list behavior; Story 2.3 and 2.4 established mutation and deterministic retry conventions. Reuse these same task feature areas and avoid introducing parallel task models.
- Keep all API routes versioned under `/api/v1` and preserve RFC 7807 Problem Details with stable app `code` and `traceId` fields.
- Ownership checks remain server-authoritative for every mutation path; caller identity must come only from authenticated context.
- Data platform remains SQL Server via EF Core and should follow existing migration and DbContext conventions.
- Deletion flow must be safe and intentional in UX. Confirmation should reduce accidental destructive actions while keeping keyboard and screen-reader usability intact.
- This story should maintain compatibility with upcoming task UI-state hardening in Story 2.6.

### API Contracts

Suggested delete contract baseline:

```
DELETE /api/v1/tasks/{taskId}
Authorization: Bearer <access-token>

HTTP/1.1 204 No Content
```

Alternative explicit response contract (if used):

```
HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12",
  "deleted": true,
  "deletedAtUtc": "2026-04-27T13:20:00Z"
}
```

Forbidden contract example:

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

- Story 2.4 introduced deterministic mutation handling and idempotency expectations in task state changes; task deletion should keep similarly stable behavior under retries and duplicate submits.
- Story 2.3 established ownership-enforced update semantics and validation conventions in `TasksController` and `TaskRepository`; delete should mirror those authorization and error contract patterns.
- Story 2.2 established active/completed list filter behavior and accessible task list interactions; deletion must reconcile list state without breaking filter semantics.

### Git Intelligence Summary

- Repository history remains implementation-artifact driven for this epic, so established conventions in stories 2.1-2.4 are the primary reference for naming, contracts, and test placement.

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

- Verify successful owned-task deletion removes task from task list responses and visible UI state.
- Verify repeated delete requests yield deterministic behavior and do not produce inconsistent list state.
- Verify unauthorized and forbidden ownership cases follow existing auth error contract patterns and never mutate protected data.
- Verify malformed route identifiers and validation failures return RFC 7807 Problem Details with stable `code` and `traceId`.
- Verify confirmation UX remains keyboard and screen-reader accessible, including deterministic focus return after cancel/confirm.
- Verify mobile and desktop interactions preserve safe confirmation flow without blocking recovery after API failure.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 2, Story 2.5]
- Functional requirements (`FR11`, `FR27`) and reliability constraints (`NFR3`, `NFR7`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md, Functional Requirements and NonFunctional Requirements]
- Product-level task lifecycle and ownership expectations: [Source: _bmad-output/planning-artifacts/prd.md, MVP - Minimum Viable Product; Task Management Functional Requirements]
- API conventions and architectural constraints (`/api/v1`, Problem Details, ownership checks, SQL Server EF Core): [Source: _bmad-output/planning-artifacts/architecture.md, API and Communication Patterns; Authentication and Security; Data Architecture]
- UX guidance for safe destructive actions, accessibility, and responsive behavior: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Accessibility Considerations; UX Consistency Patterns; Responsive Design & Accessibility]
- Previous implementation baseline: [Source: _bmad-output/implementation-artifacts/2-4-implement-task-completion-toggle-with-deterministic-state.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Added `DELETE /api/v1/tasks/{taskId}` with route GUID validation, RFC 7807 validation errors, ownership enforcement, and deterministic `204 No Content` retry behavior.
- Extended task repository contracts and implementation with ownership-scoped hard delete semantics and idempotent not-found handling.
- Added backend integration tests for owned delete success, repeated delete idempotency, malformed IDs, unauthorized access, and cross-user forbidden deletion.
- Added frontend delete operation in `TaskService`, plus an accessible confirmation dialog flow in task list UI with keyboard Escape cancel, deterministic focus return, and immediate list summary reconciliation.
- Added frontend unit tests for delete request mapping and confirmation open/cancel/confirm/error behavior.
- Story-specific tests pass; an existing non-story concurrent completion toggle test remains flaky in `TasksControllerTests` and is unchanged by this story.

### File List

- _bmad-output/implementation-artifacts/2-5-implement-task-deletion-and-safe-ux-confirmation.md
- task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs
- task-tracker-web/src/app/shared/services/task.service.ts
- task-tracker-web/src/app/shared/services/task.service.spec.ts
- task-tracker-web/src/app/features/tasks/task-list.component.ts
- task-tracker-web/src/app/features/tasks/task-list.component.html
- task-tracker-web/src/app/features/tasks/task-list.component.scss
- task-tracker-web/src/app/features/tasks/task-list.component.spec.ts
