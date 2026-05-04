# Story 6.2: Implement Moderation Actions with Safety Guards

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an administrator,
I want to apply moderation actions to protect leaderboard integrity,
so that rankings remain fair.

## Acceptance Criteria

1. Given a reviewed suspicious case, when moderation action is executed, then ranking correction/flag action is applied under policy rules.
2. Given a reviewed suspicious case, when moderation action is executed, then destructive operations require explicit confirmation.

## Tasks / Subtasks

- [x] Implement admin-only moderation command surface (AC: 1, 2)
  - [x] Add authenticated admin API endpoint(s) for moderation actions tied to suspicious case identity/correlation from Story 6.1.
  - [x] Restrict commands to admin policy only and return standardized Problem Details responses for unauthorized/forbidden/validation failures.
  - [x] Keep action contract explicit and deterministic (case id, action type, reason code/text, confirmation token/flag).

- [x] Implement policy-guarded moderation action handlers (AC: 1)
  - [x] Support initial moderation actions for this story scope: ranking correction and suspicious-entity flagging.
  - [x] Enforce policy rules (allowed transitions, target eligibility, reason required, actor role constraints).
  - [x] Ensure command processing is deterministic and prevents conflicting double-apply behavior on the same moderation intent.

- [x] Add explicit destructive-action confirmation safeguards (AC: 2)
  - [x] Require explicit confirmation for destructive/high-impact moderation operations (for example, rank correction that materially changes standings).
  - [x] Validate confirmation context server-side (actor, target, case, intended action) before commit.
  - [x] Return clear recovery guidance when confirmation is missing, stale, or invalid.

- [x] Persist privileged action audit trail and observability signals (AC: 1, 2)
  - [x] Emit immutable audit records for moderation commands with actor, target, action, reason, timestamp, and correlation id.
  - [x] Add structured logs and counters for attempted, succeeded, rejected, and failed moderation actions.
  - [x] Preserve trace correlation so future support diagnostics (Stories 6.3/6.4) can explain why a correction occurred.

- [x] Build/administer moderation UX safety flow (AC: 1, 2)
  - [x] Add moderation controls to the admin suspicious-case workspace created in Story 6.1.
  - [x] Introduce explicit confirmation UI for destructive actions (modal or equivalent) with clear consequence messaging.
  - [x] Provide optimistic-disabled states, success/failure feedback, and retry-safe behavior to avoid accidental duplicate submissions.

- [x] Add automated tests for policy rules, confirmation guards, and audit behavior (AC: 1, 2)
  - [x] Backend integration tests for admin success path, non-admin forbidden path, validation failures, and confirmation-required behavior.
  - [x] Backend tests for audit-log persistence and correlation fields on moderation actions.
  - [x] Frontend tests for confirmation flow, disabled destructive action without confirmation, and result-state rendering.

## Dev Notes

- Story 6.2 is the first privileged mutation path in Epic 6; preserve least privilege and deterministic behavior.
- Reuse suspicious-case identity and correlation conventions established by Story 6.1 to avoid parallel or incompatible moderation models.
- Keep moderation command scope narrow and auditable; defer richer policy engines/workflows to later stories unless required by AC.
- Do not bypass centralized authorization or Problem Details conventions.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs`
  - `task-tracker-api/TaskTracker.Api/Authorization/`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/ILeaderboardRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/LeaderboardRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/OperationsControllerTests.cs`

- Frontend likely touch points:
  - `task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.ts`
  - `task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.html`
  - `task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.scss`
  - `task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.spec.ts`
  - `task-tracker-web/src/app/shared/services/suspicious-cases.service.ts`
  - `task-tracker-web/src/app/shared/models/suspicious-cases.models.ts`

### Testing Requirements

- Verify moderation actions execute only for admin role and only under policy-valid conditions.
- Verify destructive actions cannot execute without explicit confirmation and clear operator intent.
- Verify moderation action outcomes are deterministic under retries/duplicate submits.
- Verify immutable audit data is recorded with required actor/target/action/reason/timestamp/correlation context.
- Verify UI states for confirm/cancel/success/error are accessible and do not allow unsafe accidental execution.

### Previous Story Intelligence

- Story 6.1 established a read-only suspicious-case surface, deterministic case ordering, and admin-only routing/policy enforcement.
- Story 6.1 already introduced suspicious-case identifiers and correlation references; moderation commands should extend these rather than redesigning case identity.
- Story 6.1 test patterns covered admin vs non-admin behavior and operational workspace states; reuse this pattern for mutation flows.

### Git Intelligence Summary

- Recent implementation trends in this repo favor additive changes, deterministic behavior, explicit policy checks, and integration tests for role boundaries.
- Continue using low-refactor, feature-focused increments to minimize regression risk in leaderboard/progression behavior.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 6, Story 6.2]
- Admin/Ops journey and moderation expectations: [Source: _bmad-output/planning-artifacts/prd.md, Journey 3: Admin/Ops User]
- Privileged action audit requirements: [Source: _bmad-output/planning-artifacts/prd.md, FR30 and Admin, Moderation, and Support Operations]
- Role/policy and Problem Details constraints: [Source: _bmad-output/planning-artifacts/architecture.md, Authentication and Security; API and Communication Patterns]
- Immutable audit and traceability constraints: [Source: _bmad-output/planning-artifacts/architecture.md, Auditability and Observability baseline]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 6.2 executed manually in Copilot chat (workflow CLI command not available in this shell)

### Completion Notes List

- Added admin-only moderation command endpoint for suspicious cases with deterministic action contracts and standardized Problem Details responses.
- Implemented policy-guarded moderation handlers for ranking correction and entity flagging with intent-key idempotency to prevent duplicate apply behavior.
- Added destructive action confirmation safeguards with actor-bound confirmation token validation and clear conflict recovery guidance.
- Persisted immutable moderation audit records and instrumentation counters/logs for attempted, succeeded, rejected, and failed operations.
- Extended suspicious-case admin UX with moderation reason composer, destructive confirmation modal, safe disabled states, and action feedback.
- Added backend integration tests and frontend component tests covering admin success, non-admin forbidden, confirmation-required, retry-safe behavior, and UI confirmation flow.

### File List

- _bmad-output/implementation-artifacts/6-2-implement-moderation-actions-with-safety-guards.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs
- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/ILeaderboardRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/LeaderboardRepository.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/ModerationActionAudit.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/User.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/OperationsControllerTests.cs
- task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.ts
- task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.html
- task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.scss
- task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.spec.ts
- task-tracker-web/src/app/shared/models/suspicious-cases.models.ts
- task-tracker-web/src/app/shared/services/suspicious-cases.service.ts
