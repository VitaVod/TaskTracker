# Story 7.1: Implement Integration Authentication and Scoped Credentials

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an integration partner,
I want authenticated, scoped access,
so that external automation can operate safely.

## Acceptance Criteria

1. Given integration credentials with scope, when integration API call is made, then request is authorized only for granted scopes.
2. Given integration credentials with scope, when integration API call is made, then unauthorized scopes return consistent forbidden errors.

## Tasks / Subtasks

- [ ] Define integration credential model and storage for scoped least-privilege access (AC: 1, 2)
  - [ ] Add persistence entity/table for integration credentials and grants (credential id, integration id/name, owner user id, allowed scopes, secret hash/key metadata, status, expiresAtUtc, revokedAtUtc, createdAtUtc, rotatedAtUtc).
  - [ ] Use SQL Server + EF Core conventions already in the project (migrations under API project persistence area, deterministic indexes for lookup by key id, owner user, and active state).
  - [ ] Ensure credential secrets are not stored in plaintext and rotation/revocation can be represented without deleting historical records.

- [ ] Implement integration authentication pipeline in API (AC: 1, 2)
  - [ ] Add an integration auth handler/service that validates presented integration credentials and resolves principal context (integration id, owner user id, granted scopes).
  - [ ] Integrate with existing API auth/error approach: preserve Problem Details format and trace id behavior.
  - [ ] Keep request correlation continuity by honoring `X-Correlation-Id` fallback patterns used in current operations/auth flows.

- [ ] Add explicit scope-based authorization checks for integration entry points (AC: 1, 2)
  - [ ] Define integration scope constants and policy checks for upcoming integration operations (prepare for Story 7.2 create/sync and later stories).
  - [ ] Enforce deny-by-default behavior when scope is missing or credential is invalid/expired/revoked.
  - [ ] Return consistent forbidden contract for insufficient scope (stable app error code + RFC 7807 payload + traceId).

- [ ] Expose initial management/read APIs for integration credentials (AC: 1)
  - [ ] Add restricted endpoint(s) for creating/listing/revoking scoped integration credentials tied to a single authorized user identity.
  - [ ] Prevent cross-user credential assignment or ownership drift.
  - [ ] Include audit-friendly fields in response contracts without exposing secrets after issuance.

- [ ] Add observability and audit hooks for integration auth decisions (AC: 1, 2)
  - [ ] Log authentication outcomes (success/invalid/revoked/expired/scope-denied) with correlation and trace information.
  - [ ] Emit counters/metrics for integration auth attempts and forbidden results by scope.
  - [ ] Ensure privileged/admin paths used for credential management remain auditable under established operations patterns.

- [ ] Add automated tests covering auth and scope enforcement (AC: 1, 2)
  - [ ] Integration tests: valid credential with required scope succeeds; missing scope returns 403 with consistent Problem Details payload.
  - [ ] Integration tests: revoked/expired/invalid credentials fail deterministically and do not leak secret material.
  - [ ] Authorization tests: ownership boundaries enforced for credential management operations.

## Dev Notes

- Epic 7 must preserve parity with first-party behavior: integration paths follow the same validation and authorization principles as internal endpoints.
- Keep implementation additive and aligned with existing auth/token/error patterns in the API (`Program.cs`, auth repositories/services, policy-based authorization).
- This story should establish secure integration credential + scope infrastructure; task create/sync behavior itself is addressed in Story 7.2.
- Keep deterministic contracts and avoid ad-hoc error shapes; use existing Problem Details + traceId conventions.
- Ensure design keeps future idempotent retry handling (Story 7.3) straightforward by including stable credential and request identity metadata.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Program.cs`
  - `task-tracker-api/TaskTracker.Api/Controllers/` (new integration controller or extension of existing module boundaries)
  - `task-tracker-api/TaskTracker.Api/Features/Auth/` (shared token/auth patterns)
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/` (new integration credential entity)
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`

- Frontend touch points are optional for this story unless credential management UI is included; if added, follow existing feature-scoped Angular patterns under `task-tracker-web/src/app/features/` and shared service/model patterns under `task-tracker-web/src/app/shared/`.

### Testing Requirements

- Validate credential authentication outcomes: valid, invalid signature/secret, expired, revoked.
- Validate scope checks for allowed vs denied operations return deterministic status and contract.
- Validate forbidden responses include stable application error code and traceId in Problem Details envelope.
- Validate ownership constraints on credential management operations (no cross-user assignment/read).
- Validate no secret disclosure in logs or API responses after credential issuance.

### Previous Story Intelligence

- Story 6.5 hardened immutable privileged audit and deterministic role-gated operations behavior. Reuse existing logging/correlation/audit style for integration auth decisions.
- Existing API surfaces already standardize Problem Details and traceId wiring; integration auth should plug into this model rather than introducing custom formats.

### Git Intelligence Summary

- Recent commits indicate additive, feature-scoped changes with strong emphasis on deterministic behavior and test coverage.
- Preserve this approach: focused backend changes, consistent contracts, and integration tests alongside implementation.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 7, Story 7.1]
- Integration requirements and least-privilege constraints: [Source: _bmad-output/planning-artifacts/prd.md, Integration Requirements]
- FR/NFR mapping for integration auth/scope/ownership: [Source: _bmad-output/planning-artifacts/epics.md, Requirements Mapping]
- API/security/error guardrails (JWT, policy auth, Problem Details, traceId): [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; API and Communication Patterns]
- Existing implementation patterns: [Source: task-tracker-api/TaskTracker.Api/Program.cs, task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 7.1 executed manually in Copilot chat (workflow CLI command not available in this shell)

### Completion Notes List

- Story scaffold created with integration-authentication and scope guardrails aligned to Epic 7 and architecture constraints.

### File List

- _bmad-output/implementation-artifacts/7-1-implement-integration-authentication-and-scoped-credentials.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
