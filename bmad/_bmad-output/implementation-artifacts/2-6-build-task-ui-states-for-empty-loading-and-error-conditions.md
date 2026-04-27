# Story 2.6: Build Task UI States for Empty, Loading, and Error Conditions

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want clear non-happy-path task states,
so that I always know what to do next.

## Acceptance Criteria

1. Given no tasks exist, when tasks page loads, then an action-oriented empty state is shown and create-task action is prominent.
2. Given load or save failures, when errors occur, then recovery actions are shown and keyboard and screen-reader paths remain usable.

## Tasks / Subtasks

- [x] Define canonical task UI state model for list surfaces (AC: 1, 2)
  - [x] Confirm and document empty, loading, success, and error states for active and completed task views.
  - [x] Ensure states are mutually exclusive and deterministic during route load, filter change, and mutation retries.
  - [x] Reuse existing task feature conventions to avoid parallel state models.

- [x] Implement action-oriented empty state in task list experience (AC: 1)
  - [x] Add explicit empty-state container with concise guidance and a primary create-task call to action.
  - [x] Ensure empty state appears for both initial account state and filtered views that return no items.
  - [x] Keep touch targets and layout behavior responsive for desktop and mobile breakpoints.

- [x] Implement loading states for task list and task mutations (AC: 2)
  - [x] Add skeleton or equivalent loading placeholders for task list regions.
  - [x] Provide deterministic in-flight indicators for save/update/delete/complete actions.
  - [x] Prevent duplicate submissions while preserving user context and focus.

- [x] Implement recoverable error states for fetch and mutation failures (AC: 2)
  - [x] Surface specific error messaging for list load failures and task mutation failures.
  - [x] Provide clear recovery actions (for example retry, dismiss, or navigate to create-task) without dead ends.
  - [x] Map backend Problem Details responses to stable, user-facing messaging patterns.

- [x] Preserve accessibility requirements for all non-happy-path states (AC: 1, 2)
  - [x] Ensure full keyboard operability for empty-state actions and error recovery controls.
  - [x] Add screen-reader announcements for loading completion and error outcomes using existing live-region patterns.
  - [x] Verify state communication does not rely on color alone.

- [x] Add frontend unit/component tests for empty, loading, and error UX behavior (AC: 1, 2)
  - [x] Test empty-state rendering conditions and create-task CTA visibility.
  - [x] Test loading-state transitions for initial fetch and in-flight mutations.
  - [x] Test recoverable error messaging and retry action wiring.

- [x] Add backend and integration regression tests needed to support predictable UI states (AC: 2)
  - [x] Verify task API error contracts remain RFC 7807 Problem Details with stable app code and traceId.
  - [x] Verify unauthorized and forbidden outcomes remain deterministic for UI error mapping.
  - [x] Verify failed operations do not leave inconsistent task state in subsequent reads.

## Dev Notes

- Story 2.1 through 2.5 established task CRUD, deterministic mutation behavior, and ownership enforcement. Story 2.6 should harden UX behavior under non-happy-path conditions without changing core domain semantics.
- Maintain API versioning and error contract consistency under `/api/v1`, including RFC 7807 Problem Details payload shape with stable `code` and `traceId` fields.
- Keep implementation aligned with SQL Server plus EF Core persistence behavior already used by task endpoints.
- UX guidance requires non-dead-end empty/error states and explicit recovery actions. Loading states should be informative without blocking critical navigation.
- Accessibility requirements remain first-class: full keyboard flow, screen-reader announcement coverage, and state communication beyond color-only signals.

### API and UI Contracts

Suggested frontend state model baseline:

```
type TaskUiState =
  | { kind: "loading" }
  | { kind: "empty"; filter: "active" | "completed" | "all" }
  | { kind: "ready"; tasks: TaskItem[] }
  | { kind: "error"; scope: "load" | "mutation"; code?: string; traceId?: string };
```

Example Problem Details mapping contract for UI error state:

```
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://api.tasktracker.local/problems/validation",
  "title": "Validation failed",
  "status": 400,
  "code": "validation.request.invalid",
  "traceId": "0HN1FDHJ..."
}
```

### Previous Story Intelligence

- Story 2.2 established active/completed list behavior and task list rendering conventions that should remain the base for empty and loading states.
- Story 2.3 established task mutation and validation error handling patterns for update flows.
- Story 2.4 established deterministic in-flight and duplicate-submit expectations with idempotent behavior for completion actions.
- Story 2.5 established accessible confirmation and error recovery patterns for destructive operations.

### Project Structure Notes

- Backend expected touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Contracts/TaskContracts.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs`

- Frontend expected touch points:
  - `task-tracker-web/src/app/features/tasks/`
  - `task-tracker-web/src/app/shared/services/task.service.ts`
  - `task-tracker-web/src/app/shared/services/task.service.spec.ts`

- Continue using the current feature-first implementation style already adopted in `TaskTracker.Api` and Angular task feature modules.

### Testing Requirements

- Verify empty-state UX appears when no tasks exist and includes a prominent create-task action.
- Verify loading-state UX appears for list fetch and mutation in-flight conditions, with deterministic transition back to ready/empty/error states.
- Verify error-state UX is recoverable with explicit retry/action controls and no dead-end screens.
- Verify keyboard and screen-reader paths are usable for empty and error actions, including state announcements.
- Verify mobile and desktop layouts preserve clarity and operability for non-happy-path states.
- Verify Problem Details `code` and `traceId` values remain available for frontend error mapping and support diagnostics.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 2, Story 2.6]
- Functional requirements (`FR9`, `FR10`, `FR11`, `FR12`, `FR13`, `FR14`, `FR27`) and non-functional requirements (`NFR3`, `NFR7`, `NFR15`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md, Functional Requirements and NonFunctional Requirements]
- Product-level performance, accessibility, and responsive behavior targets: [Source: _bmad-output/planning-artifacts/prd.md, Performance; Accessibility; Technical Assumptions and Constraints]
- API conventions and ownership/error contract constraints: [Source: _bmad-output/planning-artifacts/architecture.md, API and Communication Patterns; Authentication and Security]
- UX behavior for empty/loading/error and recovery guidance: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, UX Consistency Patterns; Accessibility Considerations; Responsive Design and Accessibility]
- Previous implementation context: [Source: _bmad-output/implementation-artifacts/2-5-implement-task-deletion-and-safe-ux-confirmation.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Story 2.6 drafted with implementation context, constraints, and testing guidance for empty/loading/error state hardening.
- Sprint status updated to set story 2.6 as `ready-for-dev`.

### File List

- _bmad-output/implementation-artifacts/2-6-build-task-ui-states-for-empty-loading-and-error-conditions.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
