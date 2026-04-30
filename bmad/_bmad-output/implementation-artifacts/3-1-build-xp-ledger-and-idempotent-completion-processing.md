# Story 3.1: Build XP Ledger and Idempotent Completion Processing

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want XP granted exactly once for an eligible completion,
so that progress feels fair and trustworthy.

## Acceptance Criteria

1. Given a task completion event with idempotency key, when progression command runs, then XP ledger entry is created at most once per eligible event.
2. Retries for the same eligible completion return a consistent result and do not produce duplicate XP grants.

## Tasks / Subtasks

- [x] Define progression command contract for XP grant from completion event (AC: 1, 2)
  - [x] Add request/response contract for processing a completion event by idempotency key and actor context.
  - [x] Define deterministic response payload fields that can be reused by follow-up streak/progress stories.
  - [x] Ensure API/app contract remains aligned with existing `/api/v1` + Problem Details conventions.

- [x] Implement idempotent XP ledger write path in backend (AC: 1, 2)
  - [x] Add or extend persistence entities for XP ledger records keyed by completion event identity.
  - [x] Enforce database uniqueness constraints/indexes to prevent duplicate XP ledger writes during retries.
  - [x] Persist only server-authoritative values (no frontend-computed XP outcomes).

- [x] Wire progression processing to completion event source of truth (AC: 1)
  - [x] Reuse `TaskCompletionEvent` data established in Story 2.4 as the trigger input for XP processing.
  - [x] Validate ownership and eligibility before writing XP records.
  - [x] Keep operation transactional so completion-to-ledger state cannot partially commit.

- [x] Return deterministic replay result for duplicate submissions (AC: 2)
  - [x] If event was already processed, return original outcome snapshot rather than creating new rows.
  - [x] Ensure repeated calls are stable across transient retries/reconnects.
  - [x] Preserve traceability fields (event id, correlation/trace id) for support diagnostics.

- [x] Add regression-safe API and persistence tests for idempotency behavior (AC: 1, 2)
  - [x] Verify single eligible completion produces exactly one XP ledger entry.
  - [x] Verify repeated processing of same idempotency key returns consistent result and no duplicate grants.
  - [x] Verify ownership/auth failures and invalid inputs map to RFC 7807 Problem Details with stable `code` and `traceId`.

- [x] Add frontend/service integration hooks needed for immediate momentum feedback (AC: 2)
  - [x] Expose deterministic completion outcome fields required by dashboard feedback components.
  - [x] Keep server state authoritative for XP/streak-related UI updates.
  - [x] Avoid introducing optimistic progression state that can diverge from backend truth.

## Dev Notes

- Story 2.4 already introduced completion idempotency and a persisted `TaskCompletionEvent`; Story 3.1 must build directly on that event path rather than creating a second completion source.
- SQL Server remains mandatory for persistence and uniqueness guarantees in this story, using EF Core SQL Server provider and migration workflow already established.
- Progression behavior must remain deterministic and replay-safe; retries should never create additional XP grants for the same eligible event.
- Preserve existing API standards: versioned routes under `/api/v1`, RFC 7807 Problem Details, and stable `code` + `traceId` fields.
- Keep ownership and authorization enforcement server-side; user-scoped progress data cannot be exposed cross-account.

### Project Structure Notes

- Backend expected touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`

- Frontend expected touch points:
  - `task-tracker-web/src/app/features/dashboard/`
  - `task-tracker-web/src/app/features/tasks/`
  - `task-tracker-web/src/app/shared/models/`
  - `task-tracker-web/src/app/shared/services/`

- Architecture planning docs define future layered projects (`TaskTracker.Application`, `TaskTracker.Domain`, `TaskTracker.Infrastructure`), but current implementation is feature-first within `TaskTracker.Api`; follow the implemented structure for this story.

### Testing Requirements

- Verify first-time eligible completion processing writes one and only one XP ledger entry.
- Verify duplicate submissions/retries for the same idempotency key replay the same result without additional XP rows.
- Verify transaction boundaries prevent partial commit states between completion-event consumption and XP ledger write.
- Verify unauthorized/forbidden ownership cases cannot mutate XP ledger state.
- Verify invalid request/idempotency inputs return RFC 7807 payload with stable `code` and `traceId`.
- Verify API response latency and deterministic behavior support UX requirement for near-immediate completion feedback.

### Git Intelligence Summary

- Recent commits show deterministic completion/idempotency patterns are already established in task flows and should be reused rather than replaced:
  - `feat(tasks): implement deterministic completion toggle with idempotency hardening`
  - `feat(tasks): implement story 2.3 task update and organizational attributes`
- Reuse existing repository and test conventions from those task stories to avoid introducing parallel progression handling styles.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 3, Story 3.1]
- Epic requirement coverage and non-functional constraints (`FR15`, `FR16`, `FR17`, `FR18`, `FR19`, `FR20`, `FR42`, `NFR3`, `NFR4`, `NFR6`, `NFR7`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory and Epic 3]
- Product-level idempotency, deterministic progression, and trust goals: [Source: _bmad-output/planning-artifacts/prd.md, MVP Success Criteria; Progress, XP, and Streaks; Non-Functional Requirements]
- Architecture constraints for SQL Server, idempotency, timezone policy, event payload shape, and consistency rules: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; Data Architecture; Communication Patterns; Enforcement Guidelines]
- UX expectations for immediate feedback, trust, and accessible status communication: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Core User Experience; Experience Principles; Accessibility]
- Previous implementation baseline: [Source: _bmad-output/implementation-artifacts/2-4-implement-task-completion-toggle-with-deterministic-state.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Added idempotent XP ledger persistence (`XpLedgerEntries`) with SQL Server migration and uniqueness constraints keyed to completion-event identity.
- Extended completion toggle contract to return deterministic progression outcome payload (`eventId`, `xpGranted`, replay flag, `idempotencyKey`, `traceId`).
- Reused Story 2.4 `TaskCompletionEvent` path and added in-process idempotency lock for stable concurrent retries in test/runtime.
- Updated backend integration tests and frontend task service/component hooks for deterministic momentum feedback without optimistic XP state.
- Validation completed with passing API tests (`67`) and web tests (`59`).

### File List

- task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Contracts/TaskContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/XpLedgerEntry.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260427181329_AddXpLedgerEntriesForStory31.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260427181329_AddXpLedgerEntriesForStory31.Designer.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/TaskTrackerDbContextModelSnapshot.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs
- task-tracker-web/src/app/shared/models/task.models.ts
- task-tracker-web/src/app/shared/services/task.service.ts
- task-tracker-web/src/app/features/tasks/task-list.component.ts
- task-tracker-web/src/app/features/tasks/task-list.component.spec.ts
- _bmad-output/implementation-artifacts/3-1-build-xp-ledger-and-idempotent-completion-processing.md
