# Story 7.4: Add Integration Observability and Failure Recovery

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an operations team member,
I want visibility into integration health,
so that failures are diagnosed and recovered quickly.

## Acceptance Criteria

1. Given integration processing activity, when telemetry is collected, then success/failure rates, retries, and error classes are observable.
2. Given integration processing activity, when telemetry is collected, then failure events include enough context for support/admin troubleshooting.

## Tasks / Subtasks

- [x] Extend integration metrics to expose health and retry/failure behavior (AC: 1)
  - [x] Keep using the existing integrations meter in IntegrationsController and add metrics for request latency and retry shape (for example first-attempt vs idempotent replay vs post-failure retry).
  - [x] Ensure metrics dimensions are low-cardinality and operationally safe (`integration_id`, `outcome`, `error_class`) and do not include user IDs, task IDs, idempotency keys, secrets, or raw correlation IDs.
  - [x] Make `validation_failed`, `forbidden`, and unexpected server failure outcomes observable in counters so support can distinguish client misuse from platform incidents.

- [x] Capture failure events with structured, queryable troubleshooting context (AC: 2)
  - [x] Add a dedicated persistence record for integration processing failures (for example `IntegrationProcessingFailureEvent`) with fields for `occurredAtUtc`, `integrationId`, `ownerUserId`, `externalTaskId`, `idempotencyKey`, `errorClass`, `errorCode`, `httpStatus`, `correlationId`, and `traceId`.
  - [x] Add EF Core mapping and SQL Server migration/indexes optimized for support lookups by time window, integration ID, owner user ID, and correlation/trace identifiers.
  - [x] Persist only troubleshooting metadata; never persist credential secrets or raw authorization headers.

- [x] Wire deterministic failure-classification and recording into create-sync flow (AC: 1, 2)
  - [x] In `POST /api/v1/integrations/tasks/create-sync`, classify failures into stable classes (for example `validation`, `authorization`, `not_found`, `conflict`, `transient_infrastructure`, `unexpected`) and map to existing Problem Details `code` conventions.
  - [x] Record failure events exactly once per request attempt with trace/correlation continuity, including paths that currently return validation or forbidden responses.
  - [x] Preserve existing successful behavior and idempotent replay semantics from Story 7.3; observability changes must be additive and must not alter mutation outcomes.

- [x] Provide privileged recovery/troubleshooting read path for integration failures (AC: 2)
  - [x] Add an operations endpoint under `/api/v1/ops` for paginated/filtered integration failure events (time range, integration ID, owner user ID, error class, correlation ID, trace ID).
  - [x] Protect the endpoint with existing support/admin policies and ensure forbidden/validation responses follow current Problem Details shape with `code` and `traceId`.
  - [x] Reuse current operations response patterns (window metadata, pagination metadata, correlation ID in response payload) to keep support tooling consistent.

- [x] Add actionable runbook-aligned recovery guidance to responses and logs (AC: 2)
  - [x] Add deterministic machine-readable error classification and a recovery hint field in integration failure responses where safe (for example retryable infrastructure issue vs non-retryable validation/auth failure).
  - [x] Keep all failure logs structured and correlation-aware so support can pivot from API trace IDs to stored failure events and ops queries.
  - [x] Confirm the UX/error guidance remains explicit and recoverable, aligned with UX requirement for non-blocking error recovery messaging.

- [x] Add automated tests for observability and failure-recovery behavior (AC: 1, 2)
  - [x] Integration tests for create-sync failure cases (missing/invalid idempotency key, forbidden scope, simulated repository exception) that assert failure classification and Problem Details contract stability.
  - [x] Integration tests for new ops failure query endpoint validating authorization, filtering, pagination, and trace/correlation fields in results.
  - [ ] Where feasible, unit tests for failure classifier logic to guarantee stable mapping from exception/result type to `errorClass` and retry guidance.

## Dev Notes

- Story 7.3 already established idempotent replay semantics and integration outcome telemetry (`created`, `updated`, `idempotent_replay`, `validation_failed`, `forbidden`) in `IntegrationsController` and replay persistence in `IntegrationEventIdempotencyRecord`.
- Story 7.4 should make observability operationally complete: measurable health trends plus durable failure context for support/admin troubleshooting and recovery.
- Keep implementation additive and backward-compatible. Do not break current integration auth, create/sync contracts, or idempotent mutation behavior.
- Reuse existing ops patterns from Story 6.x (`OperationsController`) for authorization gates, pagination, correlation/trace propagation, and metrics naming consistency.

### Project Structure Notes

- Expected backend touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/IntegrationsController.cs`
  - `task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Integrations/Contracts/IntegrationCredentialContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/` (new failure event entity)
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/IntegrationsControllerTests.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/OperationsControllerTests.cs`

- Architectural consistency requirements:
  - Keep versioned REST under `/api/v1`.
  - Preserve RFC 7807 Problem Details envelope with stable `code` and `traceId`.
  - Keep SQL Server + EF Core additive migrations and deterministic index strategy.
  - Preserve policy-based authorization and ownership boundaries for all integration and operations read paths.

### Testing Requirements

- Verify integration metrics/failure events are emitted for validation, authorization, and unexpected failure paths.
- Verify no leakage of sensitive credentials in logs or persisted failure events.
- Verify new support/admin failure query path enforces role policy and returns correlation/trace context.
- Verify replay/success flows from Story 7.3 remain unchanged while observability expands.

### Previous Story Intelligence

- Story 7.3 already records replay metadata (`operation`, `traceId`, `correlationId`) in `IntegrationEventIdempotencyRecord`; build observability on top of this model instead of creating parallel idempotency logic.
- Existing integration tests already cover sequential and concurrent replay behavior; extend that suite for failure observability without weakening deterministic replay guarantees.
- Existing operations endpoints already implement support/admin query patterns with metrics and correlation-aware responses; reuse those patterns for consistency and lower regression risk.

### Git Intelligence Summary

- Recent commits show additive, test-backed, deterministic backend evolution with stable API contracts.
- Continue this pattern: focused backend changes, additive migrations, and integration tests as source of truth for behavior.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 7, Story 7.4]
- Epic 7 objective and parity constraints: [Source: _bmad-output/planning-artifacts/epics.md, Epic 7]
- Integration requirements and observability expectations: [Source: _bmad-output/planning-artifacts/prd.md, Integration Requirements; Integrations and External Access]
- Architecture guardrails (Problem Details, traceability, observability, retries): [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; API and Communication Patterns; Process Patterns]
- Existing integration create/sync and telemetry baseline: [Source: task-tracker-api/TaskTracker.Api/Controllers/IntegrationsController.cs]
- Existing idempotent replay persistence baseline: [Source: task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/IntegrationEventIdempotencyRecord.cs]
- Existing support/admin diagnostics patterns: [Source: task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 7.4 executed manually in Copilot chat (workflow CLI command not available in this shell)

### Completion Notes List

- Added integration create-sync observability metrics for outcomes, retry-shape, and latency with low-cardinality dimensions.
- Added durable `IntegrationProcessingFailureEvent` persistence, EF mapping, and SQL migration for support troubleshooting lookups.
- Wired deterministic failure classification plus recovery hints into create-sync validation/forbidden/unexpected paths with structured correlation/trace logging.
- Added `/api/v1/ops/admin-support/integration-failures` with support/admin authorization, filtering, pagination, and correlation-aware response envelope.
- Expanded integration and operations integration tests to assert failure persistence, error classification metadata, and privileged query behaviors.

### File List

- _bmad-output/implementation-artifacts/7-4-add-integration-observability-and-failure-recovery.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- task-tracker-api/TaskTracker.Api/Controllers/IntegrationsController.cs
- task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs
- task-tracker-api/TaskTracker.Api/Features/Integrations/Contracts/IntegrationCredentialContracts.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/IntegrationProcessingFailureEvent.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260503132000_AddIntegrationFailureEventsStory74.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/IntegrationsControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/OperationsControllerTests.cs
