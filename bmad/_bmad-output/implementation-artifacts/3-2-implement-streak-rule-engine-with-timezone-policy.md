# Story 3.2: Implement Streak Rule Engine with Timezone Policy

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want streak outcomes computed accurately for my local day boundaries,
so that streak continuity feels predictable.

## Acceptance Criteria

1. Given user timezone and historical completion events, when streak evaluation runs, then result is continue, reset, or restart according to deterministic policy.
2. Streak calculations use UTC event storage with timezone projection for user-local day boundaries.

## Tasks / Subtasks

- [x] Define streak rule engine contract and deterministic decision model (AC: 1)
  - [x] Add/extend request and response contracts for streak evaluation inputs and outputs (timezone, evaluation window, result state).
  - [x] Encode outcome states as deterministic enum values (`continue`, `reset`, `restart`) with stable serialized representation.
  - [x] Ensure contract follows existing `/api/v1` and Problem Details conventions.

- [x] Implement timezone-aware streak evaluation in backend application flow (AC: 1, 2)
  - [x] Evaluate completion-event history by projecting UTC timestamps into the user-local day boundary.
  - [x] Apply deterministic gap/continuity rules to compute streak outcomes from ordered events.
  - [x] Keep evaluation repeatable and independent from server-local timezone settings.

- [x] Persist/serve authoritative streak state derived from completion history (AC: 1)
  - [x] Add or extend streak snapshot model/state needed by progression APIs in Epic 3.
  - [x] Ensure ownership checks remain server-side for all streak reads/writes.
  - [x] Keep updates transactional with progression writes where applicable.

- [x] Harden for retries, replay, and boundary conditions (AC: 1, 2)
  - [x] Ensure repeated evaluations with unchanged inputs produce identical outcomes.
  - [x] Add boundary handling for day rollover, DST transitions, and sparse activity history.
  - [x] Preserve traceability fields (event id, correlation/trace id) for support diagnostics.

- [x] Add API and domain tests for streak rule determinism and timezone projection (AC: 1, 2)
  - [x] Verify continue/reset/restart outcomes for representative completion timelines.
  - [x] Verify UTC-to-local projection behavior at edge boundaries (midnight transitions, DST changes).
  - [x] Verify unauthorized/forbidden and invalid input cases return RFC 7807 Problem Details with stable `code` and `traceId`.

- [x] Prepare frontend/service integration hooks for upcoming dashboard progress stories (AC: 1)
  - [x] Expose stable streak outcome fields expected by Story 3.3 and Story 3.4 consumers.
  - [x] Keep server state authoritative to avoid client-side streak rule divergence.
  - [x] Avoid optimistic streak continuity assumptions that can conflict with backend computation.

## Dev Notes

- This story builds on Story 3.1 completion-event and XP idempotency baseline; reuse the same progression event source and avoid creating a parallel completion timeline.
- Time semantics are mandatory architectural constraints: store events in UTC and compute user-visible streak behavior via timezone projection.
- Streak rule evaluation must be deterministic and reproducible under retries, reconnects, and repeated queries with identical inputs.
- Preserve existing API standards: versioned routes under `/api/v1`, RFC 7807 Problem Details, and stable `code` + `traceId` fields.
- Keep ownership and authorization enforcement server-side; user-scoped streak data cannot be exposed across accounts.

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

- Verify deterministic mapping to `continue`, `reset`, and `restart` across representative activity sequences.
- Verify UTC storage with timezone projection drives streak boundaries, including midnight and DST edge cases.
- Verify repeated evaluation with same inputs is replay-stable and returns unchanged outcomes.
- Verify unauthorized/forbidden ownership cases cannot read or mutate cross-user streak state.
- Verify invalid request/timezone inputs return RFC 7807 payload with stable `code` and `traceId`.
- Verify response latency and deterministic behavior support near-immediate momentum feedback expectations.

### Git Intelligence Summary

- Recent completion/idempotency implementation in Story 3.1 established progression contracts and deterministic replay behavior; Story 3.2 should extend those patterns for streak rules.
- Reuse current repository and test conventions from recent task/progression stories to avoid introducing parallel streak handling styles.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 3, Story 3.2]
- Epic requirement coverage and non-functional constraints (`FR17`, `FR18`, `FR19`, `FR20`, `FR42`, `NFR4`, `NFR6`, `NFR7`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory and Epic 3]
- Product-level progression and streak determinism goals: [Source: _bmad-output/planning-artifacts/prd.md, Progress, XP, and Streaks; Non-Functional Requirements]
- Architecture constraints for UTC storage, timezone projection, deterministic policy, and consistency rules: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; Data Architecture; Communication Patterns]
- Previous implementation baseline: [Source: _bmad-output/implementation-artifacts/3-1-build-xp-ledger-and-idempotent-completion-processing.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed manually (BMAD CLI unavailable in current shell)

### Completion Notes List

- Added deterministic streak evaluation contract and engine with stable serialized outcomes (`continue`, `reset`, `restart`).
- Integrated timezone-projected streak evaluation into completion progression flow and persisted authoritative `UserStreakSnapshot` state transactionally.
- Added SQL Server migration for streak snapshots and replay-safe response enrichment (`progression.streak`).
- Added API integration coverage for streak outcomes, idempotent replay stability, and invalid stored timezone validation behavior.
- Added unit tests for boundary scenarios including local-midnight projection and DST transition determinism.
- Extended frontend shared task models with streak hook fields for Story 3.3/3.4 consumers.
- Validation completed with passing API tests (`75`); frontend suite still has one pre-existing failure in `create-task.component.spec.ts`.

### File List

- task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Contracts/TaskContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Streaks/StreakRuleEngine.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/UserStreakSnapshot.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260428094257_AddUserStreakSnapshotsForStory32.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260428094257_AddUserStreakSnapshotsForStory32.Designer.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/TaskTrackerDbContextModelSnapshot.cs
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Unit/StreakRuleEngineTests.cs
- task-tracker-web/src/app/shared/models/task.models.ts
- _bmad-output/implementation-artifacts/3-2-implement-streak-rule-engine-with-timezone-policy.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
