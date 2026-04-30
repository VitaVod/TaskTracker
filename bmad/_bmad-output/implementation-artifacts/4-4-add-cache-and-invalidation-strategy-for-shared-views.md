# Story 4.4: Add Cache and Invalidation Strategy for Shared Views

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform owner,
I want leaderboard/stats reads to stay fast and fresh,
so that shared views scale under load.

## Acceptance Criteria

1. Given completion commits that affect shared views, when cache invalidation triggers, then leaderboard and stats cache entries are refreshed within defined freshness window.
2. Given completion commits that affect shared views, when cache invalidation triggers, then stale/duplicate view anomalies are detectable by telemetry.

## Tasks / Subtasks

- [x] Introduce distributed cache baseline for shared-view reads (AC: 1)
  - [x] Add distributed cache registration in API startup (prefer `AddStackExchangeRedisCache` when connection string exists, with local-safe fallback policy for development).
  - [x] Define cache settings/options (TTL per shared view, key prefix, and freshness window) in configuration.
  - [x] Keep contracts under `/api/v1` unchanged while enabling cache-backed repository behavior.

- [x] Add cache-backed read adapters for leaderboard and global statistics (AC: 1)
  - [x] Wrap existing leaderboard/statistics repository queries with cache-first, SQL-fallback behavior.
  - [x] Use deterministic key design for leaderboard dimensions (`type`, `page`, `pageSize`, and participation-sensitive shape) and a dedicated key for global stats.
  - [x] Ensure serialized payloads preserve existing response schema and tie-break ordering guarantees from Story 4.1.

- [x] Implement deterministic invalidation after completion commits (AC: 1)
  - [x] Invalidate shared-view cache entries only after successful persistence in completion flow (do not invalidate on failed or replay-only paths that do not change state).
  - [x] Wire invalidation from completion command path in `TaskRepository.ToggleCompletionOwnedAsync` after transaction success.
  - [x] Include invalidation for both leaderboard variants and global statistics to prevent stale cross-surface reads.

- [x] Add telemetry and diagnostics for freshness anomalies (AC: 2)
  - [x] Emit structured logs/telemetry events for cache hit, miss, invalidate, and refresh outcomes with trace correlation.
  - [x] Add anomaly signals for stale-read detection and duplicate-refresh suppression keyed by idempotency/trace context.
  - [x] Keep telemetry naming stable and feature-scoped for future dashboarding in ops/support stories.

- [x] Add backend tests covering cache, invalidation, and telemetry behavior (AC: 1, 2)
  - [x] Integration tests verify leaderboard/stat responses are reused from cache within TTL and refreshed after relevant completion commit.
  - [x] Integration tests verify idempotent replay paths do not create duplicate invalidation side effects.
  - [x] Integration tests/assertions verify telemetry/log entries include `TraceId`, cache operation type, and view scope.

## Dev Notes

- Story 4.4 extends Stories 4.1-4.3 by adding read-path performance and freshness controls without changing API contracts or privacy semantics.
- Architecture requires cache-first leaderboard/statistics reads with explicit invalidation tied to completion commits and read-model isolation from write entities.
- Invalidation must be post-commit to avoid ghost refreshes and must preserve deterministic state guarantees from the idempotent completion pipeline.
- Keep SQL Server as source of truth; cache is an acceleration layer only.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Program.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/LeaderboardRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Statistics/Repositories/GlobalStatisticsRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/LeaderboardsControllerTests.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/StatisticsControllerTests.cs`

- New cache abstractions should stay feature-scoped (for example under `Features/Leaderboards` and `Features/Statistics`) and avoid cross-feature coupling.

### Testing Requirements

- Verify leaderboard and global statistics endpoints remain contract-compatible while cache is enabled.
- Verify cache freshness window behavior and post-completion invalidation for both leaderboard types and global stats.
- Verify completion idempotency replay does not produce duplicate invalidation side effects.
- Verify telemetry/logging captures hit/miss/invalidate/refresh with correlation identifiers.
- Verify no regression to privacy-safe identity behavior and pagination determinism.

### Previous Story Intelligence

- Story 4.3 established stable global statistics contracts and dashboard loading/error patterns; Story 4.4 must preserve those response shapes while adding acceleration.
- Story 4.1 and 4.2 established deterministic ordering and participation/privacy behavior for leaderboard reads; caching must not bypass those constraints.

### Git Intelligence Summary

- Recent work favors deterministic backend behavior, explicit Problem Details handling, and integration-first validation.
- Story 4.4 should follow the same pattern: preserve public contracts, add cache behavior behind repository boundaries, and validate with integration coverage.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 4, Story 4.4]
- Shared-view and freshness requirements (`FR21`-`FR26`, `FR28`, `NFR5`, `NFR6`, `NFR14`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory]
- Performance/scalability and telemetry expectations for leaderboard/stat views: [Source: _bmad-output/planning-artifacts/prd.md, Non-Functional Requirements]
- Architecture cache strategy and invalidation policy: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; Data Architecture; Integration Points]
- Existing implementation baselines: [Source: _bmad-output/implementation-artifacts/4-1-implement-streak-and-completed-task-leaderboard-read-models.md, _bmad-output/implementation-artifacts/4-2-implement-privacy-safe-public-identity-and-participation-controls.md, _bmad-output/implementation-artifacts/4-3-build-global-statistics-endpoints-and-ui-panels.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed manually (repo skill workflow; CLI does not expose create-story command)

### Completion Notes List

- Added `SharedViewCacheOptions` and optional Redis-backed distributed cache registration with development-safe in-memory fallback.
- Implemented feature-scoped `ISharedViewCacheCoordinator` with deterministic keying, generation-based invalidation, freshness window enforcement, and structured telemetry for `cache.miss`, `cache.hit`, `cache.refresh`, `cache.invalidate`, plus anomaly events.
- Wrapped leaderboard/statistics repositories with cache-first behavior while preserving `/api/v1` contracts and deterministic ordering semantics.
- Wired post-commit shared-view invalidation from `TaskRepository.ToggleCompletionOwnedAsync` only when completion state changes were persisted successfully.
- Added integration coverage for leaderboard/statistics cache reuse + refresh after completion commits, replay-safe invalidation behavior, and cache telemetry assertions with trace correlation.
- Executed `dotnet test TaskTracker.sln` with all tests passing (98/98).

### File List

- _bmad-output/implementation-artifacts/4-4-add-cache-and-invalidation-strategy-for-shared-views.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/TaskTracker.Api/TaskTracker.Api.csproj
- task-tracker-api/TaskTracker.Api/appsettings.json
- task-tracker-api/TaskTracker.Api/appsettings.Development.json
- task-tracker-api/TaskTracker.Api/Features/SharedViews/Caching/SharedViewCacheOptions.cs
- task-tracker-api/TaskTracker.Api/Features/SharedViews/Caching/ISharedViewCacheCoordinator.cs
- task-tracker-api/TaskTracker.Api/Features/SharedViews/Caching/SharedViewCacheCoordinator.cs
- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/LeaderboardRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Statistics/Repositories/GlobalStatisticsRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/LeaderboardsControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/StatisticsControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
