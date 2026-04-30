# Story 4.1: Implement Streak and Completed-Task Leaderboard Read Models

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to compare ranking by streak and completed tasks,
so that I can benchmark my momentum.

## Acceptance Criteria

1. Given ranking requests for supported leaderboard types, when leaderboard endpoints are called, then deterministic rank ordering and tie-break rules are applied.
2. Given ranking requests for supported leaderboard types, when leaderboard endpoints are called, then responses are paginated and performance-aware.

## Tasks / Subtasks

- [x] Define leaderboard API contracts for supported rank types (AC: 1, 2)
  - [x] Add versioned endpoint contracts under `/api/v1` for streak and completed-task leaderboards.
  - [x] Define request query parameters for leaderboard type, page number, and page size with explicit validation boundaries.
  - [x] Define response model fields including rank, public identity placeholder field, metric value, and pagination metadata.

- [x] Implement deterministic leaderboard read-model queries (AC: 1)
  - [x] Add repository/query methods that compute rank order for streak and completed-task leaderboards.
  - [x] Apply deterministic tie-break rules (for example metric desc, then stable secondary key) so repeated calls produce consistent ordering.
  - [x] Ensure ownership/privacy-sensitive fields remain excluded pending Story 4.2 policy enforcement.

- [x] Expose leaderboard endpoints in API layer with auth and validation (AC: 1, 2)
  - [x] Add/extend controller endpoint(s) for leaderboard retrieval and map query validation failures to Problem Details.
  - [x] Enforce authenticated access and server-side authorization checks consistent with existing protected API patterns.
  - [x] Return paginated responses with deterministic metadata (`page`, `pageSize`, `totalCount`, `hasNextPage`).

- [x] Implement performance-aware pagination and query safeguards (AC: 2)
  - [x] Enforce sane maximum page size and default page values.
  - [x] Add query/index considerations for ranking reads to keep shared-view response targets on track.
  - [x] Capture trace/correlation fields in logs for leaderboard read diagnostics.

- [x] Add backend tests for ranking determinism and pagination behavior (AC: 1, 2)
  - [x] Integration tests for streak leaderboard ordering including tie scenarios.
  - [x] Integration tests for completed-task leaderboard ordering including tie scenarios.
  - [x] Integration tests for pagination boundaries, default values, invalid parameters, and deterministic repeated responses.

## Dev Notes

- Story 4.1 introduces leaderboard read models only. Privacy-safe public identity shaping and participation controls are handled in Story 4.2.
- Reuse existing feature-first backend structure in `TaskTracker.Api` and avoid introducing partially migrated architecture layers.
- Keep response semantics deterministic under retries/reloads to align with existing product reliability expectations.
- Preserve API consistency standards: versioned REST contracts and Problem Details error mapping.

### Project Structure Notes

- Expected backend touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/LeaderboardsController.cs` (new or equivalent)
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Contracts/LeaderboardContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/ILeaderboardRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/LeaderboardRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/LeaderboardsControllerTests.cs`

- Optional frontend touch points (if endpoint wiring is added during this story):
  - `task-tracker-web/src/app/shared/services/leaderboard.service.ts`
  - `task-tracker-web/src/app/shared/models/leaderboard.models.ts`

### Testing Requirements

- Verify streak leaderboard endpoint returns deterministic ranking and stable tie-break behavior.
- Verify completed-task leaderboard endpoint returns deterministic ranking and stable tie-break behavior.
- Verify pagination metadata correctness for first/middle/last pages and boundary conditions.
- Verify invalid pagination/type parameters map to expected Problem Details responses.
- Verify repeated requests over unchanged data return consistent ordering.

### Previous Story Intelligence

- Story 3.3 established progress snapshot API and ownership guardrails that should be mirrored for leaderboard read access patterns.
- Story 3.5 reinforced deterministic presentation expectations and should inform response stability for social ranking views.

### Git Intelligence Summary

- Epic 3 closed with deterministic progression logic and strong integration-test coverage patterns.
- Story 4.1 should follow the same pattern: server-authoritative ranking logic plus explicit tie-break and pagination tests.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 4, Story 4.1]
- Leaderboard requirement context (`FR21`, `FR22`, `FR26`, `NFR5`, `NFR7`, `NFR14`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory]
- Product social-momentum goals: [Source: _bmad-output/planning-artifacts/prd.md, Functional Requirements; Success Criteria]
- Architecture constraints for shared views and deterministic refresh behavior: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions]
- UX guidance for leaderboard row semantics and accessibility: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, UX Design Requirements]
- Prior implementation baselines: [Source: _bmad-output/implementation-artifacts/3-3-expose-progress-apis-for-xp-streak-and-trend-snapshots.md, _bmad-output/implementation-artifacts/3-5-implement-momentum-summary-and-historical-progress-view.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed manually (BMAD CLI unavailable in current shell)

### Completion Notes List

- Added leaderboard contracts and `/api/v1/leaderboards` endpoint with authenticated access, validation boundaries, and RFC 7807 Problem Details mapping.
- Implemented deterministic leaderboard read-model queries for streak and completed tasks with tie-break order (`metric desc`, then `userId asc`) and paginated rank metadata.
- Added query safeguards (`page`, `pageSize` defaults and max bounds), trace-aware request logging, and leaderboard-friendly persistence indexes.
- Added integration tests for streak/completed tie scenarios, pagination first/middle/last page metadata, defaults, invalid query handling, unauthorized access, and repeated deterministic responses.
- Validation completed with passing backend tests (`89` passed).

### File List

- task-tracker-api/TaskTracker.Api/Controllers/LeaderboardsController.cs
- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Contracts/LeaderboardContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/ILeaderboardRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/LeaderboardRepository.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/LeaderboardsControllerTests.cs
- _bmad-output/implementation-artifacts/4-1-implement-streak-and-completed-task-leaderboard-read-models.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
