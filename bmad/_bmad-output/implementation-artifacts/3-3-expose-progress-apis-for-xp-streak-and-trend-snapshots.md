# Story 3.3: Expose Progress APIs for XP, Streak, and Trend Snapshots

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to view current progression status and trend data,
so that I can monitor momentum over time.

## Acceptance Criteria

1. Given authenticated progress request, when XP/streak/summary endpoints are called, then current totals and trend snapshots are returned with bounded latency.
2. Ownership checks prevent cross-user access for all progress endpoints.

## Tasks / Subtasks

- [x] Define and publish Progress API contracts for XP total, streak snapshot, and trend summary (AC: 1)
  - [x] Add or extend request/response DTOs for progress reads with stable field naming and explicit types.
  - [x] Keep contracts aligned with existing `/api/v1` route/versioning and Problem Details error style.
  - [x] Ensure response shape is deterministic and replay-safe across repeated requests.

- [x] Implement authenticated progress read endpoints in backend API layer (AC: 1, 2)
  - [x] Add endpoints for current XP summary, current streak status, and trend snapshot aggregation.
  - [x] Keep reads user-scoped using server-side identity extraction and ownership enforcement.
  - [x] Return bounded-size payloads with pagination/windowing where trend data can grow.

- [x] Build progress query services/repositories over existing progression data (AC: 1)
  - [x] Reuse Story 3.1 ledger and Story 3.2 streak artifacts as source of truth.
  - [x] Implement deterministic trend window calculations (daily/weekly summary) using UTC storage and timezone projection.
  - [x] Avoid duplicate or divergent projection logic between API handlers.

- [x] Enforce security and authorization invariants for cross-user isolation (AC: 2)
  - [x] Reject anonymous requests with standard unauthorized responses.
  - [x] Reject access to data not owned by authenticated principal.
  - [x] Ensure no endpoint accepts externally supplied user identifiers that bypass identity context.

- [x] Add API and integration tests for progress endpoint behavior and reliability (AC: 1, 2)
  - [x] Verify authenticated user receives correct XP/streak/trend values from seeded progression history.
  - [x] Verify cross-user access attempts are blocked (401/403 as appropriate).
  - [x] Verify invalid parameters produce RFC 7807 Problem Details with stable `code` and `traceId`.
  - [x] Verify bounded-latency expectation with representative dataset sizes used in current test environment.

- [x] Prepare frontend service integration surfaces for Story 3.4 consumers (AC: 1)
  - [x] Add or update typed client models/services required to consume new progress endpoints.
  - [x] Keep client mapping resilient to optional/new server fields.
  - [x] Ensure polling/refresh strategy does not create inconsistent visible state.

## Dev Notes

- This story depends on Story 3.1 and Story 3.2 outputs; do not introduce a second source of truth for XP or streak state.
- API behavior must preserve determinism and consistency under retries/reconnects to support trustworthy momentum signals.
- Keep endpoint design aligned with existing conventions: `/api/v1` versioning, RFC 7807 Problem Details, and traceable error metadata.
- Latency target is tied to user-visible momentum flow; optimize query shape and payload bounds before adding complexity.
- Server-side ownership and authorization checks are mandatory for all progression read paths.

### Project Structure Notes

- Backend expected touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`

- Frontend expected touch points:
  - `task-tracker-web/src/app/features/dashboard/`
  - `task-tracker-web/src/app/shared/services/`
  - `task-tracker-web/src/app/shared/models/`

- Current implementation remains feature-first within `TaskTracker.Api`; apply architecture intent without forcing an unplanned structural migration in this story.

### Testing Requirements

- Verify endpoint outputs are deterministic for unchanged underlying progression data.
- Verify trend snapshot calculations are correct across timezone day boundaries and representative historical windows.
- Verify unauthorized and cross-user scenarios are blocked with standard API error semantics.
- Verify response size/performance remain within bounded expectations for current MVP data volume.
- Verify frontend service integration models parse and expose progress payloads required by Story 3.4 components.

### Git Intelligence Summary

- Story 3.1 established idempotent completion and XP ledger baseline; Story 3.2 established deterministic timezone-aware streak computation.
- Story 3.3 should compose read-facing progress APIs from existing progression state rather than duplicating write-path logic.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 3, Story 3.3]
- Epic requirement coverage (`FR15`, `FR16`, `FR17`, `FR18`, `FR19`, `FR20`, `FR42`, `NFR4`, `NFR6`, `NFR7`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md]
- Product progression and momentum context: [Source: _bmad-output/planning-artifacts/prd.md, Functional Requirements and Success Metrics]
- Architecture constraints for versioned API, deterministic processing, security ownership checks, and UTC/timezone handling: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions]
- Prior implementation baselines: [Source: _bmad-output/implementation-artifacts/3-1-build-xp-ledger-and-idempotent-completion-processing.md, _bmad-output/implementation-artifacts/3-2-implement-streak-rule-engine-with-timezone-policy.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed manually (BMAD CLI unavailable in current shell)

### Completion Notes List

- Added authenticated progress read endpoints under `/api/v1/progress` for XP summary, streak snapshot, and trend summary.
- Implemented deterministic and bounded trend aggregation over existing XP ledger/completion data with timezone projection and daily/weekly windows.
- Added integration tests for ownership isolation, unauthorized access handling, trend validation errors, and deterministic response shape.
- Added frontend typed models and a progress service surface for Story 3.4 dashboard consumers.
- Validation completed with passing API tests (`82`) and frontend tests (`63`).

### File List

- task-tracker-api/TaskTracker.Api/Controllers/ProgressController.cs
- task-tracker-api/TaskTracker.Api/Features/Progress/Contracts/ProgressContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Progress/Repositories/IProgressRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Progress/Repositories/ProgressRepository.cs
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/ProgressControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-web/src/app/shared/models/progress.models.ts
- task-tracker-web/src/app/shared/services/progress.service.ts
- task-tracker-web/src/app/shared/services/progress.service.spec.ts
- _bmad-output/implementation-artifacts/3-3-expose-progress-apis-for-xp-streak-and-trend-snapshots.md
- _bmad-output/implementation-artifacts/sprint-status.yaml