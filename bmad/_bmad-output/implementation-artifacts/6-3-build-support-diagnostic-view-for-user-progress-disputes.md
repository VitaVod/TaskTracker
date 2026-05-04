# Story 6.3: Build Support Diagnostic View for User Progress Disputes

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a support user,
I want read-only visibility into user task/progress state,
so that I can resolve reported issues quickly.

## Acceptance Criteria

1. Given a support investigation request, when support view loads for a user, then relevant account/task/xp/streak snapshots are displayed read-only.
2. Given a support investigation request, when support view loads for a user, then support role cannot mutate protected user data.

## Tasks / Subtasks

- [x] Implement support-only diagnostic query surface (AC: 1, 2)
  - [x] Add authenticated support endpoint(s) that return consolidated user diagnostic snapshots (account summary, task state, XP totals, streak state, and recent progression-relevant markers).
  - [x] Restrict access with explicit support-role policy checks and standardized Problem Details responses for unauthorized/forbidden requests.
  - [x] Keep endpoint contract read-only and bounded (targeted lookup, optional date/window filters, deterministic ordering, and safe payload limits).

- [x] Build consolidated read model for dispute triage (AC: 1)
  - [x] Define a support diagnostic DTO that joins existing task/progress/leaderboard-supporting projections without duplicating core progression logic.
  - [x] Include data points needed to explain outcomes (current streak snapshot, XP snapshot, recent completions, timezone context, and derived state timestamps).
  - [x] Exclude unnecessary private fields and enforce privacy-safe projections while still enabling practical troubleshooting.

- [x] Add support diagnostic UI (AC: 1, 2)
  - [x] Add a support-only route/screen where an authorized support agent can load a single user's read-only diagnostic panel.
  - [x] Present clear read-only sections for account state, tasks/progress summaries, and troubleshooting hints, with loading/empty/error states.
  - [x] Ensure the UI does not render mutation controls (no edit/delete/moderation actions) for support role.

- [x] Add observability and traceability for support investigations (AC: 1, 2)
  - [x] Emit structured logs for diagnostic-view requests with actor id/role, target user id, and correlation id.
  - [x] Add counters/metrics for successful lookups, no-data lookups, and forbidden access attempts.
  - [x] Align telemetry shape with Epic 6 trust/ops conventions so Story 6.4 timeline correlation can reuse identifiers.

- [x] Add automated tests for read-only enforcement and snapshot rendering (AC: 1, 2)
  - [x] Backend integration tests for support-role success and non-support forbidden behavior.
  - [x] Backend tests validating payload shape, deterministic ordering, and read-only contract behavior.
  - [x] Frontend tests validating support-route guard behavior and read-only diagnostic rendering across loading/empty/error states.

## Dev Notes

- Story 6.3 introduces support troubleshooting visibility only; it must not introduce privileged mutation paths.
- Reuse suspicious-case and moderation trace/correlation conventions from Stories 6.1 and 6.2 where possible to preserve operational continuity.
- Use existing progression/streak computation outputs as source-of-truth read models; do not re-implement XP/streak business rules in support controllers.
- Keep support troubleshooting deterministic and explainable so dispute handling is consistent before Story 6.4 timeline depth is added.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs`
  - `task-tracker-api/TaskTracker.Api/Authorization/`
  - `task-tracker-api/TaskTracker.Api/Features/Progress/`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`

- Frontend likely touch points:
  - `task-tracker-web/src/app/app.routes.ts`
  - `task-tracker-web/src/app/features/` (support diagnostics feature)
  - `task-tracker-web/src/app/shared/guards/`
  - `task-tracker-web/src/app/shared/services/`
  - `task-tracker-web/src/app/shared/models/`

### Testing Requirements

- Verify support users can view read-only diagnostic snapshots for a target user and that content includes task/progress context needed for disputes.
- Verify non-support users are forbidden from support diagnostics APIs and routes.
- Verify support diagnostics remain read-only (no mutation controls in UI; no mutation endpoints exposed/used by support flow).
- Verify logs/metrics capture actor/target/correlation context without exposing unnecessary PII.

### Previous Story Intelligence

- Story 6.1 established deterministic suspicious-case investigation patterns and admin-only operational read surfaces.
- Story 6.2 introduced privileged mutation controls and immutable audit conventions that support flows should reference but not invoke.
- Existing tests in Epic 6 already cover role-gated operational routes and can be extended for support-role read-only behavior.

### Git Intelligence Summary

- Recent Epic 6 implementation favored additive changes, deterministic contracts, explicit policy checks, and integration test coverage for role boundaries.
- Continue low-refactor, contract-first implementation to reduce regression risk in progression and leaderboard behavior.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 6, Story 6.3]
- Support dispute-resolution journey context: [Source: _bmad-output/planning-artifacts/prd.md, Journey 4: Support User]
- Internal role and privileged action requirements (`FR29`, `FR30`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory]
- Authorization, Problem Details, and observability constraints: [Source: _bmad-output/planning-artifacts/architecture.md, Authentication and Security; API and Communication Patterns]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 6.3 executed manually in Copilot chat (workflow CLI command not available in this shell)

### Completion Notes List

- Added support diagnostics API at `/api/v1/ops/support/users/{userId}` with bounded query parameters (`windowDays`, `markerLimit`) and explicit support-policy enforcement.
- Implemented consolidated read-only diagnostics payload with account, task, XP, streak, and deterministic recent markers while excluding sensitive fields (for example password hash).
- Added support diagnostics observability with structured logging and counters for success/empty/forbidden requests plus correlation id propagation.
- Added integration tests for support success path, non-support forbidden behavior, query validation, payload shape, and marker ordering.
- Added support-only Angular route, guard, data service, models, and read-only diagnostics screen with loading/empty/error states.
- Added frontend tests for support guard and diagnostics component rendering/error/validation states.
- Verified implementation with:
  - `dotnet test TaskTracker.sln --no-restore --filter "FullyQualifiedName~OperationsControllerTests"`
  - `npx ng test --watch=false --browsers=ChromeHeadless --no-progress`

### File List

- _bmad-output/implementation-artifacts/6-3-build-support-diagnostic-view-for-user-progress-disputes.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/OperationsControllerTests.cs
- task-tracker-web/src/app/app.routes.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.spec.ts
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.ts
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.html
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.scss
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.spec.ts
- task-tracker-web/src/app/shared/guards/support.guard.ts
- task-tracker-web/src/app/shared/guards/support.guard.spec.ts
- task-tracker-web/src/app/shared/models/support-diagnostics.models.ts
- task-tracker-web/src/app/shared/services/support-diagnostics.service.ts
