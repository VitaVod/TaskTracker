# Story 2.4: Implement Task Completion Toggle with Deterministic State

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to mark tasks complete and incomplete as allowed by policy,
so that my task status accurately reflects reality.

## Acceptance Criteria

1. Given a valid owned task, when completion action is sent, then task state transitions deterministically and emits completion event for progression engine.
2. Duplicate submissions do not create conflicting task state.

## Tasks / Subtasks

- [x] Add backend completion-toggle API contracts and endpoint support (AC: 1, 2)
  - [x] Extend `TaskTracker.Api.Features.Tasks.Contracts` with a completion-toggle request/response contract that carries desired completion state and idempotency key input.
  - [x] Add a completion endpoint under `/api/v1/tasks/{taskId}/completion` in `TasksController` using a mutation verb aligned with existing API style.
  - [x] Validate route/task ownership context, request payload, and idempotency key presence/format; return RFC 7807 Problem Details for invalid input.

- [x] Implement deterministic completion toggle behavior with ownership enforcement (AC: 1, 2)
  - [x] Extend `ITaskRepository` and `TaskRepository` with a completion-toggle path scoped to authenticated owner only.
  - [x] Ensure completion/incompletion updates are deterministic for repeated submissions with the same idempotency key (no conflicting task state).
  - [x] Return normalized task payload including `isCompleted` and refreshed `updatedAtUtc` so frontend state can reconcile without full reload.

- [x] Persist completion event signal for downstream progression processing (AC: 1)
  - [x] Record completion event metadata needed by later XP/streak stories (task id, owner id, resulting completion state, idempotency key, timestamp).
  - [x] Ensure duplicate command submissions do not produce duplicate progression-triggering completion events.
  - [x] Keep event naming and shape consistent with architecture guidance (for example `TaskCompleted` semantics for completion transitions).

- [x] Build frontend completion-toggle interaction in task list UI (AC: 1, 2)
  - [x] Add keyboard-accessible completion controls to task list rows for active and completed tasks.
  - [x] Disable repeated submit while toggle request is in-flight and reconcile UI from server-confirmed result.
  - [x] Preserve responsive behavior and explicit state labels so completion status is understandable beyond color cues.

- [x] Preserve auth and ownership boundary behavior (AC: 1)
  - [x] Require authenticated user context on completion-toggle endpoint.
  - [x] Return unauthorized/forbidden outcomes with consistent auth error contract patterns from previous stories.
  - [x] Add regression coverage proving cross-user completion toggles are denied and do not mutate data.

- [x] Add backend integration tests for deterministic toggles and idempotency behavior (AC: 1, 2)
  - [x] Test successful completion toggle for owned task, including deterministic response payload shape.
  - [x] Test duplicate submissions with same idempotency key produce stable result without conflicting state.
  - [x] Test invalid idempotency key/payload/ownership cases return stable Problem Details with `code` and `traceId`.

- [x] Add frontend unit/component tests for completion controls and duplicate-submit prevention (AC: 1, 2)
  - [x] Test request mapping and response reconciliation in `TaskService` for completion toggle operations.
  - [x] Test in-flight disable behavior to prevent repeated submissions from keyboard/mouse/touch paths.
  - [x] Test accessible labels/announcements for completion state changes and error recovery messaging.

## Dev Notes

- Story 2.1 established task create contracts and ownership persistence behavior; Story 2.2 established active/completed list states; Story 2.3 established update mutation patterns and validation conventions. Reuse these same task feature surfaces and avoid parallel task models.
- Keep all API routes versioned under `/api/v1` and preserve RFC 7807-style error contracts with stable application `code` and `traceId`.
- Completion actions are part of the core motivation loop and must stay deterministic under retries/duplicates; idempotency is required for completion command paths.
- Ownership checks remain server-authoritative for every mutation path; caller identity must come only from authenticated context.
- Data platform remains SQL Server via EF Core and should follow existing persistence and migration conventions.
- This story is a dependency bridge into Epic 3 progression processing; completion event signal shape must remain forward-compatible with XP/streak workflows.

### API Contracts

Suggested completion-toggle contract baseline:

```
PATCH /api/v1/tasks/{taskId}/completion
Authorization: Bearer <access-token>
Content-Type: application/json
Idempotency-Key: 1f4c4f1b-4c8a-4f12-8f53-9a8d10cb6d75

{
  "isCompleted": true
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
  "isCompleted": true,
  "createdAtUtc": "2026-04-25T11:30:12Z",
  "updatedAtUtc": "2026-04-27T09:15:03Z"
}
```

Duplicate submission behavior example:

```
PATCH /api/v1/tasks/{taskId}/completion
Idempotency-Key: 1f4c4f1b-4c8a-4f12-8f53-9a8d10cb6d75
{
  "isCompleted": true
}

HTTP/1.1 200 OK
Content-Type: application/json

{
  "id": "7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12",
  "isCompleted": true,
  "updatedAtUtc": "2026-04-27T09:15:03Z"
}
```

Invalid request contract example:

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
    "idempotencyKey": ["Idempotency-Key header is required for completion toggle."]
  }
}
```

### Previous Story Intelligence

- Story 2.3 implemented mutation patterns in `TasksController`, `TaskContracts`, and `TaskRepository` with ownership and Problem Details conventions; completion toggle should follow the same coding style and error-handling contracts.
- Story 2.3 frontend edit flow added robust in-place state reconciliation and error preservation behavior in task list experience; completion toggle should reuse this approach for deterministic UI updates.
- Story 2.2 established active/completed filtering behavior and accessible state labels; completion toggles must keep list-state semantics and announcement patterns coherent.

### Git Intelligence Summary

- Repository history remains shallow (baseline plus incremental story work), so implementation artifacts and current code are the most reliable convention source for this story.

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

- Verify successful completion toggle on owned task persists deterministic state and returns normalized payload.
- Verify duplicate submissions with the same idempotency key never create conflicting task state or duplicate completion event signal.
- Verify invalid payload/idempotency values return RFC 7807 Problem Details with stable `code` and `traceId`.
- Verify unauthorized and forbidden ownership cases follow consistent auth error contracts and do not mutate data.
- Verify frontend completion controls remain keyboard/touch accessible and prevent repeated submissions while in-flight.
- Verify mobile and desktop task list behavior preserves clear state transitions and accessible completion feedback.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 2, Story 2.4]
- Functional requirements (`FR12`, `FR14`, `FR27`) and non-functional constraints (`NFR3`, `NFR7`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md, Functional Requirements and NonFunctional Requirements]
- Product-level completion and ownership requirements: [Source: _bmad-output/planning-artifacts/prd.md, Functional Requirements; Performance Expectations; Security and Access Control]
- API conventions, idempotency, and ownership boundaries: [Source: _bmad-output/planning-artifacts/architecture.md, API and Communication Patterns; Implementation Patterns and Consistency Rules; Data Architecture]
- UX guidance for immediate feedback, accessibility, and responsive completion interactions: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Core User Experience; Accessibility Considerations; Responsive Design & Accessibility]
- Previous implementation baseline: [Source: _bmad-output/implementation-artifacts/2-3-implement-task-update-and-organizational-attributes.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Added `PATCH /api/v1/tasks/{taskId}/completion` with route, payload, and `Idempotency-Key` GUID validation returning RFC 7807 Problem Details with stable `code` and `traceId`.
- Implemented ownership-scoped deterministic toggle flow in `TaskRepository` with command replay detection by `(TaskId, OwnerId, IdempotencyKey)` and immutable response state under duplicate key retries.
- Added persistent completion signal storage via `TaskCompletionEvents` table/entity and EF Core migration, recording event metadata for downstream progression stories and preventing duplicate `TaskCompleted` emissions.
- Added backend integration tests for successful toggle, idempotent duplicate submissions, invalid payload/header handling, unauthorized access, and cross-user forbidden behavior.
- Added frontend completion controls to task list cards with accessible labels, per-task in-flight disable guards, server-confirmed state reconciliation, and live region announcements.
- Added frontend tests for completion toggle API header mapping, component reconciliation logic, and duplicate-submit prevention while request is in-flight.
- Verification: `dotnet test task-tracker-api/tests/TaskTracker.Api.Tests/TaskTracker.Api.Tests.csproj` (61 passed), `npx ng test --watch=false --browsers=ChromeHeadless` (50 passed).

### File List

- task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Contracts/TaskContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/TaskCompletionEvent.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260427094206_AddTaskCompletionEvents.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260427094206_AddTaskCompletionEvents.Designer.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/TaskTrackerDbContextModelSnapshot.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs
- task-tracker-web/src/app/features/tasks/task-list.component.ts
- task-tracker-web/src/app/features/tasks/task-list.component.html
- task-tracker-web/src/app/features/tasks/task-list.component.scss
- task-tracker-web/src/app/features/tasks/task-list.component.spec.ts
- task-tracker-web/src/app/shared/models/task.models.ts
- task-tracker-web/src/app/shared/services/task.service.ts
- task-tracker-web/src/app/shared/services/task.service.spec.ts
