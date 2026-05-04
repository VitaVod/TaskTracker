# Story 6.1: Build Admin Suspicious-Activity Review Workspace

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an administrator,
I want to review abnormal completion/ranking patterns,
so that I can detect potential abuse.

## Acceptance Criteria

1. Given ranking and activity anomaly signals, when admin review page loads, then suspicious cases are listed with relevant context.
2. Given ranking and activity anomaly signals, when admin review page loads, then access is restricted to admin role.

## Tasks / Subtasks

- [x] Implement admin-only suspicious-case query surface (AC: 1, 2)
  - [x] Add an authenticated admin endpoint that returns suspicious leaderboard/activity cases with deterministic sorting (for example: highest anomaly score, newest first tie-break).
  - [x] Enforce role-based authorization with explicit admin policy checks and Problem Details error responses for forbidden access.
  - [x] Keep the query read-only and bounded with paging/filter constraints suitable for operational review.

- [x] Build suspicious-case read model with relevant review context (AC: 1)
  - [x] Define case payload fields required for first-pass triage (case id, user/public identity, anomaly type, signal summary, computed severity, timestamps, correlation reference).
  - [x] Reuse existing leaderboard/progress aggregates where possible instead of duplicating ranking calculations.
  - [x] Ensure context excludes unnecessary PII while preserving enough diagnostics for admin investigation.

- [x] Add admin review workspace UI (AC: 1, 2)
  - [x] Add an admin route/screen that lists suspicious cases with severity/status chips and concise context cards/table rows.
  - [x] Implement loading/empty/error states with clear operator actions (refresh, retry, filter reset).
  - [x] Ensure route guard and navigation visibility are restricted to admin role only.

- [x] Add observability and audit-aligned tracing for review access (AC: 2)
  - [x] Emit structured logs for suspicious-case list retrieval attempts and outcomes, including actor id/role and trace or correlation id.
  - [x] Add counters/metrics for case-list queries, empty-result frequency, and forbidden access attempts.
  - [x] Ensure privileged review access aligns with audit-readiness conventions for upcoming moderation actions.

- [x] Add automated tests for access control and case rendering behavior (AC: 1, 2)
  - [x] Backend integration tests verifying admin access success and non-admin forbidden responses.
  - [x] Backend tests verifying suspicious-case payload shape, sorting, and pagination behavior.
  - [x] Frontend tests verifying admin-only route guard behavior and suspicious-case list rendering across loading/empty/error states.

## Dev Notes

- Story 6.1 establishes the first operational trust surface for Epic 6 and should remain read-only; moderation mutations are deferred to Story 6.2.
- Reuse existing role-policy and ownership authorization baseline from Epic 1 (especially admin policy wiring and Problem Details behavior).
- Keep suspicious-case logic deterministic and explainable so support/moderation actions in later stories can reference the same case identity/correlation.
- Prefer additive implementation over refactors in ranking/progress cores to minimize regression risk for public leaderboard behavior.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs`
  - `task-tracker-api/TaskTracker.Api/Controllers/LeaderboardsController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/ILeaderboardRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/LeaderboardRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Program.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`

- Frontend likely touch points:
  - `task-tracker-web/src/app/app.routes.ts`
  - `task-tracker-web/src/app/core/guards/`
  - `task-tracker-web/src/app/features/leaderboards/`
  - `task-tracker-web/src/app/shared/services/`
  - `task-tracker-web/src/app/shared/models/`

### Testing Requirements

- Verify suspicious cases are listed with required context and deterministic ordering when anomalies exist.
- Verify non-admin users receive forbidden responses and cannot access the review UI route.
- Verify admin review workspace handles loading, empty, and error states predictably.
- Verify logs/metrics include traceability context for privileged review reads without exposing excessive PII.

### Previous Story Intelligence

- Epic 4 delivered leaderboard read models, privacy-safe public identity controls, and shared-view caching that should be reused for anomaly context assembly.
- Epic 5 added operational diagnostics patterns and deterministic delivery tracing that can inform observability conventions for admin review tooling.
- Existing internal role baseline from Epic 1 should be reused for admin policy enforcement and standardized Problem Details responses.

### Git Intelligence Summary

- Current implementation trends favor additive, deterministic behavior with explicit integration tests and low-refactor risk.
- Scope for 6.1 should stay focused on read-only suspicious-case visibility and admin-only access control, reserving corrective mutations for Story 6.2.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 6, Story 6.1]
- Functional requirement mapping (`FR31`) and internal-role access baseline (`FR29`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory]
- Product trust and integrity operations context: [Source: _bmad-output/planning-artifacts/prd.md, User Journey 3: Admin/Ops User]
- Architecture constraints for policy-based authorization, Problem Details, and observability: [Source: _bmad-output/planning-artifacts/architecture.md, Authentication and Security; API and Communication Patterns]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 6.1 executed manually (workflow CLI command not available in this shell)

### Completion Notes List

- Added read-only suspicious-case query support in leaderboard repository with deterministic ordering and privacy-safe identity projection.
- Added admin-only suspicious-case endpoint at /api/v1/ops/admin/suspicious-cases with explicit policy authorization checks, bounded query validation, structured logs, and counters for total/empty/forbidden reads.
- Added frontend admin review workspace route, role-based guard, suspicious-cases service/model layer, and operator-focused loading/empty/error list UX.
- Added backend integration tests and frontend unit tests for authorization, sorting/paging behavior, and state rendering.
- Verification completed with successful .NET and Angular test runs.

### File List

- task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs
- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/ILeaderboardRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/LeaderboardRepository.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/OperationsControllerTests.cs
- task-tracker-web/src/app/app.routes.ts
- task-tracker-web/src/app/features/leaderboards/leaderboard.component.html
- task-tracker-web/src/app/features/leaderboards/leaderboard.component.scss
- task-tracker-web/src/app/features/leaderboards/leaderboard.component.spec.ts
- task-tracker-web/src/app/features/leaderboards/leaderboard.component.ts
- task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.html
- task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.scss
- task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.spec.ts
- task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.ts
- task-tracker-web/src/app/shared/guards/admin.guard.spec.ts
- task-tracker-web/src/app/shared/guards/admin.guard.ts
- task-tracker-web/src/app/shared/models/suspicious-cases.models.ts
- task-tracker-web/src/app/shared/services/auth.service.spec.ts
- task-tracker-web/src/app/shared/services/auth.service.ts
- task-tracker-web/src/app/shared/services/suspicious-cases.service.ts
- _bmad-output/implementation-artifacts/6-1-build-admin-suspicious-activity-review-workspace.md
- _bmad-output/implementation-artifacts/sprint-status.yaml