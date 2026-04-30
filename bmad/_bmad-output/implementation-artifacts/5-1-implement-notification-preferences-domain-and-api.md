# Story 5.1: Implement Notification Preferences Domain and API

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to configure reminder and account-notification preferences,
so that communication matches my needs.

## Acceptance Criteria

1. Given authenticated user preference changes, when preference API is called, then preferences are persisted per user.
2. Given authenticated user preference changes, when preference API is called, then defaults and validation rules are enforced.

## Tasks / Subtasks

- [ ] Add notification preference domain model and persistence mapping (AC: 1, 2)
  - [ ] Add user-scoped notification preference fields/entity in `TaskTrackerDbContext` with explicit defaults for reminder and account-notification channels.
  - [ ] Add EF Core migration with SQL Server-safe defaults and non-null constraints to prevent ambiguous preference state.
  - [ ] Keep existing user/account schema behavior stable (no breaking changes to existing account profile/settings contracts).

- [ ] Implement authenticated preferences read and update API contracts (AC: 1, 2)
  - [ ] Add API contracts under `/api/v1` for reading and updating notification preferences in a deterministic shape.
  - [ ] Enforce authenticated, ownership-scoped access only (no externally supplied user id).
  - [ ] Return RFC 7807 Problem Details with stable `code` and `traceId` for validation and authorization failures.

- [ ] Implement defaults and validation guardrails in backend application flow (AC: 2)
  - [ ] Enforce supported preference values only (for example, reminder enabled/disabled and supported reminder cadence values if present).
  - [ ] Ensure missing optional fields resolve to documented defaults rather than null/implicit behavior drift.
  - [ ] Preserve idempotent update semantics so repeated identical payloads do not create divergent state.

- [ ] Integrate with existing account/settings and email foundations without duplicating behavior (AC: 1, 2)
  - [ ] Reuse existing auth/account ownership, logging, and validation patterns from `AccountController` and account validators.
  - [ ] Reuse transactional email abstractions from Story 1.6 (`ITransactionalEmailService`, result semantics) as the baseline for downstream stories.
  - [ ] Avoid introducing reminder dispatch logic in this story; focus only on preference domain and API foundation required by Story 5.2.

- [ ] Add backend tests for persistence, defaults, and authz constraints (AC: 1, 2)
  - [ ] Integration tests for authenticated read/update success and persistence across requests.
  - [ ] Integration tests for unauthorized/cross-user access rejection and stable Problem Details shape.
  - [ ] Tests for default value behavior on first read and invalid payload rejection.

- [ ] Add frontend service/model surface for Story 5 follow-up consumption (AC: 1)
  - [ ] Add typed client models/service methods in Angular shared services for notification preferences endpoints.
  - [ ] Keep account settings feature wiring minimal and non-breaking; full UX expansion can continue in later story scope.
  - [ ] Add unit tests for the new service contract mapping and error behavior.

## Dev Notes

- Story 5.1 is a foundation story for Epic 5; it should establish preference storage and API contracts that Story 5.2 (reminder pipeline) and Story 5.5 (transactional notification flow integration) can consume without schema churn.
- Existing backend patterns already enforce `/api/v1` versioning, ownership checks, and RFC 7807-style errors with `code` and `traceId`; new endpoints should match these conventions exactly.
- Existing email delivery abstractions from Story 1.6 should be reused; do not create parallel email service abstractions for preferences.
- SQL Server remains the source of truth for preference state and should store explicit defaults to prevent null-driven behavior differences.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/TaskTracker.Api/Controllers/` (new or existing account-scoped endpoint)
  - `task-tracker-api/TaskTracker.Api/Features/Account/` and/or a new `Features/Notifications/` module
  - `task-tracker-api/TaskTracker.Api/Program.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`

- Frontend likely touch points:
  - `task-tracker-web/src/app/shared/services/`
  - `task-tracker-web/src/app/shared/models/`
  - `task-tracker-web/src/app/features/account/` (if settings UI is extended)

### Testing Requirements

- Verify authenticated users can read and update only their own notification preferences.
- Verify first-read default behavior is deterministic for users without prior saved preferences.
- Verify invalid preference payloads return stable validation errors with `code` and `traceId`.
- Verify persistence is stable across repeated updates and does not regress existing account settings behavior.
- Verify Angular service contract mapping for read/update endpoints and API error handling.

### Previous Story Intelligence

- Stories 4.1-4.5 reinforced deterministic behavior, stable contracts, and privacy-safe user handling; Story 5.1 should keep these standards for preference state and API contracts.
- Story 1.6 already established transactional email abstractions and retry semantics; this story should build preference state that those delivery flows can consume, not replace.

### Git Intelligence Summary

- Recent implementation trends favor additive, contract-preserving changes with integration-test coverage and explicit traceable error contracts.
- Story 5.1 should follow the same pattern: additive schema/API work with ownership-safe enforcement and regression tests around account/auth behavior.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 5, Story 5.1]
- Epic-level requirement mapping (`FR46`, plus downstream dependency context `FR45`, `FR47`, `FR48`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory]
- Product scope baseline for reminder and notification preference controls: [Source: _bmad-output/planning-artifacts/prd.md, Product Scope]
- Architecture constraints for SQL Server persistence, `/api/v1` contracts, authz enforcement, and Problem Details: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; API and Communication Patterns; Authentication and Security]
- Prior email and security flow baseline: [Source: _bmad-output/implementation-artifacts/1-6-implement-password-recovery-and-critical-transactional-email.md]
- Existing account settings patterns and ownership-safe update flow: [Source: task-tracker-api/TaskTracker.Api/Controllers/AccountController.cs]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed manually (BMAD CLI command not available in this shell)

### Completion Notes List

### File List

- _bmad-output/implementation-artifacts/5-1-implement-notification-preferences-domain-and-api.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
