# Story 7.2: Implement Task Create/Sync Endpoint for Integrations

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an integration partner,
I want to create or sync tasks for an authorized user,
so that external planning systems can feed Task Tracker.

## Acceptance Criteria

1. Given valid integration payload mapped to a single authorized user, when create/sync operation executes, then tasks are created or updated under that user ownership only.
2. Given valid integration payload mapped to a single authorized user, when create/sync operation executes, then payload validation uses same domain rules as first-party task flows.

## Tasks / Subtasks

- [x] Replace create-sync placeholder with deterministic create/update behavior in integration endpoint (AC: 1, 2)
  - [x] Refactor `POST /api/v1/integrations/tasks/create-sync` in `IntegrationsController` from stubbed accepted response to actual task mutation response semantics.
  - [x] Resolve owner identity from integration auth claims and enforce that all mutations use that resolved user ID only.
  - [x] Preserve correlation and trace behavior (`X-Correlation-Id` fallback to `TraceIdentifier`) in both logs and response contracts.

- [x] Introduce integration task sync contract with first-party parity (AC: 2)
  - [x] Extend integration contracts beyond `ExternalTaskId` and `Title` to include first-party task attributes (`description`, `dueAtUtc`, `priority`, `category`, completion intent if required by design).
  - [x] Add explicit request rules for required and optional fields, including normalization to the same canonical values used by first-party task APIs.
  - [x] Add deterministic response model that indicates whether operation path was `created` or `updated` and includes task identity metadata.

- [x] Implement repository/service path for integration upsert by external task key (AC: 1)
  - [x] Add a stable integration mapping strategy so one external task ID resolves to exactly one owned task record.
  - [x] Add repository operation(s) for owned integration upsert that create on first sync and update on subsequent sync for same external task ID + integration identity.
  - [x] Ensure updates cannot cross user boundaries even if payload or external key collides with another owner.

- [x] Add persistence support for external sync identity and lookup performance (AC: 1)
  - [x] Extend data model with integration sync identity fields/table(s) needed to persist `(ownerUserId, integrationId, externalTaskId) -> taskId` mapping.
  - [x] Add SQL Server EF Core migration and indexes for deterministic lookup and collision prevention.
  - [x] Maintain additive migration discipline aligned with current `TaskTrackerDbContext` and migration folder conventions.

- [x] Reuse first-party validation and ownership semantics (AC: 2)
  - [x] Reuse task validation rules already enforced for first-party create/update (title length, allowed priority/category values, date constraints, trimming/normalization).
  - [x] Ensure validation errors are emitted with existing Problem Details envelope shape and stable application code conventions.
  - [x] Keep unauthorized/forbidden handling aligned with existing integration auth and scope policies.

- [x] Add observability for integration create/sync outcomes (AC: 1)
  - [x] Emit structured logs for create vs update path, integration ID, owner user ID, external task ID, correlation ID, and trace ID.
  - [x] Add counters/metrics for accepted create-sync requests and outcome classes (created/updated/validation_failed/forbidden).
  - [x] Avoid logging sensitive credential material.

- [x] Add automated tests for create/sync behavior and parity guarantees (AC: 1, 2)
  - [x] Extend integration controller integration tests to verify first request creates and repeated request for same external ID updates same task.
  - [x] Add tests that assert ownership boundary safety (integration credential for user A cannot mutate user B tasks).
  - [x] Add tests for validation parity and forbidden behavior with Problem Details code + traceId presence.

## Dev Notes

- Story 7.1 already established integration authentication, scoped credential issuance, and `IntegrationTaskCreateSync` policy. Story 7.2 should convert the current create-sync endpoint from acknowledgement-only to real owned task mutation.
- The endpoint currently accepts payload but returns `202 Accepted` without persistence. This story should define and implement the authoritative create/update behavior and response contract.
- Keep implementation parity with first-party task domain behavior instead of introducing a separate ruleset.
- Preserve deterministic behavior and lay clear groundwork for Story 7.3 idempotent retry handling.

### Project Structure Notes

- Current touch points identified:
  - `task-tracker-api/TaskTracker.Api/Controllers/IntegrationsController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Integrations/Contracts/IntegrationCredentialContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/ITaskRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/IntegrationsControllerTests.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs`

- Architectural consistency requirements:
  - Keep versioned REST under `/api/v1` and Problem Details error envelopes with `code` and `traceId` extensions.
  - Keep SQL Server + EF Core migration workflow.
  - Preserve policy-based authorization and least-privilege scope checks.

### Testing Requirements

- Validate create path: new `(integrationId, ownerUserId, externalTaskId)` creates a single owned task.
- Validate sync path: repeated create-sync with same key updates existing task and does not create duplicates.
- Validate ownership isolation: credentials tied to one owner cannot create or update another owner's tasks.
- Validate request validation parity with first-party task flows and deterministic Problem Details shape.
- Validate forbidden and unauthorized paths preserve existing codes for scope/auth failures.

### Previous Story Intelligence

- Story 7.1 added integration auth scheme, scoped credential lifecycle, and integration policy gates.
- Existing integration tests already cover scope allow/deny and credential validity states for the create-sync endpoint; extend these to cover real mutation outcomes.
- Existing task repository patterns already implement ownership-first mutations and deterministic state handling; reuse those patterns instead of bypassing repository boundaries.

### Git Intelligence Summary

- Recent implementation trends favor additive, test-backed, deterministic changes with stable API contracts.
- Continue with feature-scoped backend changes and integration tests as the source of behavioral truth.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 7, Story 7.2]
- Integration constraints and parity requirements: [Source: _bmad-output/planning-artifacts/prd.md, Integration Requirements; Integrations and External Access]
- Architecture guardrails (REST, Problem Details, SQL Server, auth policies): [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; API and Communication Patterns; Data Architecture]
- Existing integration endpoint and contracts: [Source: task-tracker-api/TaskTracker.Api/Controllers/IntegrationsController.cs, task-tracker-api/TaskTracker.Api/Features/Integrations/Contracts/IntegrationCredentialContracts.cs]
- Existing first-party task validation/mutation patterns: [Source: task-tracker-api/TaskTracker.Api/Controllers/TasksController.cs, task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs]
- Existing integration test baseline: [Source: task-tracker-api/tests/TaskTracker.Api.Tests/Integration/IntegrationsControllerTests.cs]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 7.2 executed manually in Copilot chat (workflow CLI command not available in this shell)

### Completion Notes List

- Story scaffold created with concrete implementation guidance for converting integration create-sync from placeholder response to owned create/update semantics.
- Guidance anchors integration behavior to first-party task validation and ownership rules while preparing for idempotent retry support in Story 7.3.

### File List

- _bmad-output/implementation-artifacts/7-2-implement-task-create-sync-endpoint-for-integrations.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
