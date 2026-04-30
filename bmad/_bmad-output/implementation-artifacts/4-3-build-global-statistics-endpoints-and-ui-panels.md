# Story 4.3: Build Global Statistics Endpoints and UI Panels

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want platform-wide totals for created and completed tasks,
so that I can see ecosystem activity.

## Acceptance Criteria

1. Given stats request, when global stats endpoint is called, then total created and total completed counters are returned.
2. Given stats request, when global stats endpoint is called, then UI renders stats panels with loading/error states.

## Tasks / Subtasks

- [x] Define global statistics API contract and endpoint (AC: 1)
  - [x] Add a versioned endpoint under `/api/v1` for global platform statistics and align with existing auth/authorization policy for shared progress views.
  - [x] Define response fields for total created and total completed counters with clear semantics and stable naming.
  - [x] Ensure invalid request scenarios map to existing Problem Details conventions.

- [x] Implement deterministic global counters in read-model/repository layer (AC: 1)
  - [x] Add query/repository methods that compute `totalTasksCreated` and `totalTasksCompleted` from authoritative persistence sources used by current task/progression flows.
  - [x] Define and document counter inclusion semantics (for example treatment of soft-deleted tasks and completion state transitions) to avoid ambiguous totals.
  - [x] Keep read path performance-aware with proper query/index usage consistent with shared-view latency targets.

- [x] Expose stats endpoint through API layer with observability hooks (AC: 1)
  - [x] Add controller route and response mapping in the existing feature-first API structure.
  - [x] Include trace/correlation context in logs to support shared-view diagnostics and support investigations.
  - [x] Ensure response shape remains stable for frontend consumption and future cache layering in Story 4.4.

- [x] Build frontend global stats panels with resilient states (AC: 2)
  - [x] Add/update stats models and service methods in shared frontend data layer.
  - [x] Implement dashboard/global stats UI panels that render created/completed totals with accessible labels and deterministic formatting.
  - [x] Implement loading, empty-safe fallback, and error states with action-oriented retry guidance.

- [x] Add integration and UI tests for stats behavior and state rendering (AC: 1, 2)
  - [x] Backend integration tests for endpoint response contract, counter correctness, and authenticated access behavior.
  - [x] Frontend tests for loading and error state rendering and successful stats panel display.
  - [x] Regression checks to ensure stats endpoint changes do not break existing leaderboard/progress surfaces.

## Dev Notes

- Story 4.3 extends Epic 4 shared visibility by adding global statistics read surfaces, and should stay consistent with deterministic read patterns established in Stories 4.1 and 4.2.
- Keep all contracts under `/api/v1` and preserve Problem Details error behavior used across existing API endpoints.
- Stats counters should be server-authoritative and computed from canonical data paths already used by task and progression flows; avoid client-side derivation.
- Cache/invalidation optimization is explicitly handled in Story 4.4, so this story should keep implementation cache-ready without introducing conflicting freshness semantics.

### Project Structure Notes

- Backend expected touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/StatisticsController.cs` (or equivalent feature controller)
  - `task-tracker-api/TaskTracker.Api/Features/Statistics/Contracts/GlobalStatisticsContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Statistics/Repositories/IGlobalStatisticsRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Statistics/Repositories/GlobalStatisticsRepository.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/StatisticsControllerTests.cs`

- Frontend expected touch points:
  - `task-tracker-web/src/app/shared/services/statistics.service.ts`
  - `task-tracker-web/src/app/shared/models/statistics.models.ts`
  - `task-tracker-web/src/app/features/dashboard` (or existing shared-metrics panel location)

### Testing Requirements

- Verify global stats endpoint returns both total created and total completed counters with stable field names.
- Verify returned totals are deterministic and consistent with persisted task/progression state.
- Verify API validation/authentication behavior and Problem Details responses match project conventions.
- Verify UI shows loading and error states and renders final counters with accessible labels.
- Verify stats panel integration does not regress existing dashboard, leaderboard, or progress views.

### Previous Story Intelligence

- Story 4.1 established deterministic shared-read patterns, tie-break stability, and pagination-aware API design; use the same disciplined contract approach for global stats.
- Story 4.2 introduced privacy-safe participation and cache-freshness expectations for shared views; Story 4.3 must not bypass these shared-view consistency patterns when wiring UI and backend reads.

### Git Intelligence Summary

- Recent Epic 4 stories followed server-authoritative read models with strong integration coverage and explicit contract stability.
- Story 4.3 should continue that pattern: backend-first deterministic counters, explicit endpoint contract, and resilient UI state handling.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 4, Story 4.3]
- Shared-view requirements context (`FR24`, `FR25`, `FR26`, `NFR5`, `NFR14`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory]
- Product ecosystem-activity visibility goals: [Source: _bmad-output/planning-artifacts/prd.md, Functional Requirements; Success Criteria]
- Architecture constraints for shared read models, caching readiness, and observability: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions]
- UX guidance for loading/error states and accessible status communication: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, UX Design Requirements; Accessibility Considerations]
- Prior implementation baselines: [Source: _bmad-output/implementation-artifacts/4-1-implement-streak-and-completed-task-leaderboard-read-models.md, _bmad-output/implementation-artifacts/4-2-implement-privacy-safe-public-identity-and-participation-controls.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed manually (BMAD CLI unavailable in current shell)

### Completion Notes List

- Created Story 4.3 implementation artifact with acceptance criteria, task breakdown, and architecture/testing guardrails.
- Advanced sprint tracking state for Story 4.3 from `backlog` to `ready-for-dev`.
- Implemented `/api/v1/statistics/global` with authenticated access, deterministic `totalTasksCreated` / `totalTasksCompleted` counters, and trace-aware controller logging.
- Added backend integration coverage for success, unauthorized Problem Details behavior, and logging assertions, while running leaderboard regression tests.
- Added frontend statistics model/service wiring and dashboard global activity panels with loading/error states and retry flow.
- Added dashboard component test coverage for global stats happy path, error handling, and completion rate formatting.

### File List

- _bmad-output/implementation-artifacts/4-3-build-global-statistics-endpoints-and-ui-panels.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- task-tracker-api/TaskTracker.Api/Controllers/StatisticsController.cs
- task-tracker-api/TaskTracker.Api/Features/Statistics/Contracts/GlobalStatisticsContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Statistics/Repositories/IGlobalStatisticsRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Statistics/Repositories/GlobalStatisticsRepository.cs
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/StatisticsControllerTests.cs
- task-tracker-web/src/app/shared/models/statistics.models.ts
- task-tracker-web/src/app/shared/services/statistics.service.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.spec.ts
