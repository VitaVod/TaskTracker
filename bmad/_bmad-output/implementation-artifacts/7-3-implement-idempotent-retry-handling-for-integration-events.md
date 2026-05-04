# Story 7.3: Implement Idempotent Retry Handling for Integration Events

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform owner,
I want deterministic retry behavior for integration requests,
so that repeated events do not corrupt state.

## Acceptance Criteria

1. Given duplicate or retried integration events, when processing occurs, then idempotency key handling prevents duplicate mutations.
2. Given duplicate or retried integration events, when processing occurs, then responses indicate prior success where applicable.

## Tasks / Subtasks

- [x] Add explicit idempotency contract for integration create/sync requests (AC: 1, 2)
  - [x] Require a deterministic idempotency key header on `POST /api/v1/integrations/tasks/create-sync` and validate format/length using the same strictness as existing completion idempotency paths.
  - [x] Define normalization rules (trim, case behavior, canonical storage form) and reject missing/invalid keys with existing RFC 7807 validation envelope (`code` + `traceId`).
  - [x] Extend integration request/response contracts to surface replay semantics (for example an explicit `idempotentReplay` flag or stable operation status) while preserving backward-compatible response shape where possible.

- [x] Introduce integration event idempotency persistence with SQL uniqueness guardrails (AC: 1)
  - [x] Add a dedicated persistence model to record processed integration idempotency keys scoped by owner and integration identity.
  - [x] Persist enough replay metadata to reproduce a prior-success response deterministically (task id, operation path, timestamps, correlation/trace linkage).
  - [x] Add EF Core migration and unique index strategy for SQL Server that enforces one successful mutation per `(ownerUserId, integrationId, idempotencyKey)`.

- [x] Implement deterministic repository/controller replay behavior (AC: 1, 2)
  - [x] Update integration sync repository flow to short-circuit duplicate requests by returning previously persisted success outcome instead of reapplying task mutations.
  - [x] Handle concurrent duplicate requests using existing unique-constraint handling conventions (`2601` / `2627`) and return deterministic replay output instead of surfacing transient server errors.
  - [x] Ensure replay path preserves ownership boundaries and cannot leak cross-user or cross-integration outcomes.

- [x] Preserve parity with first-party idempotency patterns (AC: 1)
  - [x] Reuse established idempotency handling patterns from task completion (`Idempotency-Key` validation, deterministic replay semantics, unique index-backed deduplication) instead of inventing separate behavior.
  - [x] Keep mutation side effects (task updates, timestamps, any downstream domain updates) single-apply under retries.
  - [x] Keep Problem Details and authorization behavior consistent with existing integration/auth endpoints.

- [x] Add observability signals for retries and replay outcomes (AC: 2)
  - [x] Extend integration metrics and logs to distinguish `created`, `updated`, `idempotent_replay`, and error outcomes.
  - [x] Include integration ID, owner user ID, external task ID, idempotency key, correlation ID, and trace ID in structured logs while avoiding secret leakage.
  - [x] Ensure replay events are visible for Story 7.4 observability work without introducing duplicate success counters.

- [x] Add automated tests for sequential and concurrent retry scenarios (AC: 1, 2)
  - [x] Integration tests: repeated create-sync request with same idempotency key returns deterministic replay semantics and does not create additional mutations.
  - [x] Integration tests: concurrent duplicate requests with same key are race-safe and produce exactly one mutation plus replay response(s).
  - [x] Integration tests: retries with different idempotency keys continue to behave as intended (new mutation/update path), and invalid key/missing key returns validation problem details.

## Dev Notes

- Story 7.2 implemented deterministic owner-scoped create/update behavior keyed by `(ownerUserId, integrationId, externalTaskId)`. Story 7.3 adds request-level deduplication for retried delivery events on top of that mapping.
- Keep implementation additive: preserve current create-sync behavior and extend it with replay-aware idempotency rather than changing ownership/validation fundamentals.
- The platform already has robust idempotency handling patterns in task completion flows; mirror those proven patterns for integration events to reduce implementation risk.
- Design response semantics now so Story 7.4 can measure retries/replays clearly without ambiguity.

### Project Structure Notes

- Expected backend touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/IntegrationsController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Integrations/Contracts/IntegrationCredentialContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/` (new idempotency record entity)
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/IntegrationsControllerTests.cs`

- Architecture consistency requirements:
  - Keep versioned REST under `/api/v1`.
  - Preserve RFC 7807 Problem Details shape with stable `code` and `traceId` extensions.
  - Keep SQL Server + EF Core additive migrations and unique index dedupe patterns.
  - Keep policy-based authorization and ownership checks as the authority for integration mutation flows.

### Testing Requirements

- Verify duplicate request replay behavior for same idempotency key with stable response semantics.
- Verify no duplicate task mutations on sequential retry and concurrent retry.
- Verify replay behavior remains owner-scoped and integration-scoped (no cross-tenant leakage).
- Verify missing/invalid idempotency key is rejected with deterministic validation response contract.
- Verify outcome telemetry/logging includes replay outcome classification and correlation identifiers.

### Previous Story Intelligence

- Story 7.2 already introduced owner-scoped integration upsert behavior and unique sync binding guarantees; build retry idempotency on top of this baseline rather than replacing it.
- Existing tests already cover create/update paths, owner isolation, and validation/authorization behavior for integration sync; extend this suite for replay semantics and race conditions.
- Current repository code already handles SQL unique violations for integration upsert and for completion idempotency in other flows; reuse this deterministic exception-handling pattern.

### Git Intelligence Summary

- Recent implementation trend favors additive, test-backed, deterministic behavior with stable API contracts.
- Keep changes focused on integration create-sync path plus persistence/test updates required for replay safety.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 7, Story 7.3]
- Epic 7 objective and integration parity expectations: [Source: _bmad-output/planning-artifacts/epics.md, Epic 7]
- Integration idempotency requirement baseline: [Source: _bmad-output/planning-artifacts/prd.md, Domain-Specific Requirements; Integration Requirements]
- Architecture guardrails (idempotent command handling, SQL indexes, Problem Details): [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; Implementation Patterns and Consistency Rules]
- Existing integration create/sync path to extend: [Source: task-tracker-api/TaskTracker.Api/Controllers/IntegrationsController.cs]
- Existing integration repository upsert behavior: [Source: task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs]
- Existing first-party idempotency validation pattern: [Source: task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs]
- Existing integration tests baseline: [Source: task-tracker-api/tests/TaskTracker.Api.Tests/Integration/IntegrationsControllerTests.cs]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 7.3 executed manually in Copilot chat (workflow CLI command not available in this shell)

### Completion Notes List

- Story scaffold created with concrete idempotency and replay guardrails for integration create/sync retries.
- Guidance aligns integration retry handling with existing deterministic idempotency patterns used in first-party mutation flows.

### File List

- _bmad-output/implementation-artifacts/7-3-implement-idempotent-retry-handling-for-integration-events.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
