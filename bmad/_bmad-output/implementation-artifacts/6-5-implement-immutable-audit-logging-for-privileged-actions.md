# Story 6.5: Implement Immutable Audit Logging for Privileged Actions

Status: review

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a compliance-minded operator,
I want privileged actions captured immutably,
so that accountability is maintained.

## Acceptance Criteria

1. Given admin/support privileged actions, when action is completed, then audit record stores actor, target, action, reason, timestamp, and correlation ID.
2. Given admin/support privileged actions, when action is completed, then audit records are queryable and tamper-resistant per policy.

## Tasks / Subtasks

- [ ] Expand immutable privileged-audit data model and persistence rules (AC: 1, 2)
  - [ ] Introduce a dedicated privileged audit entity/table for admin and support actions (do not rely only on moderation-specific records) with immutable columns for actorId, actorRole, targetUserId, actionType, reasonCode, reasonText, occurredAtUtc, correlationId, and traceId.
  - [ ] Apply EF Core migration and indexes for query patterns (`targetUserId+occurredAtUtc`, `actorUserId+occurredAtUtc`, `occurredAtUtc`) and append-only guarantees (no update/delete paths in app layer).
  - [ ] Preserve SQL Server-first compatibility and existing naming/contract conventions.

- [ ] Capture privileged operations through a centralized audit writer (AC: 1)
  - [ ] Implement an application service or repository abstraction used by all admin/support mutation flows to append audit records atomically with the privileged action outcome.
  - [ ] Include correlation and trace continuity (`X-Correlation-Id`/resolved correlation plus `HttpContext.TraceIdentifier`) and enforce required reason/action metadata before write.
  - [ ] Ensure failed or rejected privileged attempts are logged with policy-compliant outcome markers without leaking sensitive internals in API responses.

- [ ] Provide query endpoints/read models for audit retrieval (AC: 2)
  - [ ] Add role-restricted API endpoint(s) under operations/admin-support scope to query audit records with bounded filters (actor, target, actionType, date range, pagination).
  - [ ] Return deterministic ordering and RFC 7807 Problem Details for validation/forbidden errors.
  - [ ] Ensure support read flows remain read-only and do not expose mutation controls.

- [ ] Extend operations UI with audit visibility for authorized roles (AC: 2)
  - [ ] Add an audit history panel/table in existing internal operations surfaces (admin/support diagnostics) with filters for actor/target/action/date and clear empty/loading/error states.
  - [ ] Preserve keyboard accessibility, focus order, and responsive behavior per existing support/admin patterns.
  - [ ] Display trace/correlation identifiers and reason metadata in a scannable format for dispute and compliance review.

- [ ] Add observability and compliance signals (AC: 1, 2)
  - [ ] Emit structured logs and counters/histograms for audit write attempts, successes, validation rejects, forbidden access, and query latency.
  - [ ] Add guardrails against duplicate writes for retry scenarios where intent keys already exist in moderation flow.
  - [ ] Document retention/tamper-resistance assumptions in code comments or runbook notes where policy behavior is enforced.

- [ ] Add automated tests for immutability, access control, and query correctness (AC: 1, 2)
  - [ ] Backend integration tests: privileged success writes required fields; unauthorized users forbidden; invalid filters return Problem Details with stable code + traceId.
  - [ ] Backend tests: append-only enforcement (no update/delete code paths), deterministic query ordering, and pagination/filter behavior.
  - [ ] Frontend tests: audit view renders expected fields, filter interactions work, and read-only/accessibility behavior remains intact.

## Dev Notes

- This story should consolidate privileged-action auditing beyond moderation-specific records so compliance evidence is complete for both admin and support operations.
- Existing code already includes `ModerationActionAudit` and timeline integration; avoid duplicating logic by extracting reusable privileged-audit write/read components and adapting moderation paths to them.
- Keep all privileged endpoints role-protected (`admin` and/or `support` by policy) and preserve centralized Problem Details + traceId patterns.
- Tamper resistance in this project means append-only behavior at application layer plus constrained schema usage and role-limited query access; avoid introducing any API or service method that updates/deletes historical audit entries.
- Align with SQL Server and EF Core conventions already used by this repository.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/ModerationActionAudit.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/*`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/OperationsControllerTests.cs`

- Frontend likely touch points:
  - `task-tracker-web/src/app/features/ops-suspicious-cases/ops-suspicious-cases.component.ts`
  - `task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.ts`
  - `task-tracker-web/src/app/shared/services/suspicious-cases.service.ts`
  - `task-tracker-web/src/app/shared/services/support-diagnostics.service.ts`
  - `task-tracker-web/src/app/shared/models/suspicious-cases.models.ts`
  - `task-tracker-web/src/app/shared/models/support-diagnostics.models.ts`

### Testing Requirements

- Verify each privileged admin/support action emits exactly one immutable audit entry with actor, target, action, reason, occurredAtUtc, correlationId, and traceId.
- Verify non-admin/support callers cannot query privileged audit history.
- Verify date/action/actor/target filters are validated and bounded; invalid ranges produce standardized Problem Details with traceId.
- Verify query ordering is deterministic and stable under pagination.
- Verify existing moderation and support timeline functionality is not regressed while introducing generalized privileged-audit capabilities.

### Previous Story Intelligence

- Story 6.4 established correlation-aware timeline querying, deterministic ordering, and support read-only UX constraints; reuse those query and validation patterns for audit retrieval.
- Story 6.2 already writes moderation audit rows and enforces reason/confirmation safeguards; this story should generalize that audit approach rather than reinventing a separate mechanism.
- Story 6.1 and 6.3 established role-gated admin/support workspaces; keep audit views inside these existing internal surfaces for consistency.

### Git Intelligence Summary

- Recent commits indicate additive implementation with explicit role checks, deterministic API contracts, and integration tests around controllers.
- Preserve the current approach: make focused changes in operations and persistence layers with test coverage first-class, avoiding broad refactors.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 6, Story 6.5]
- Privileged action audit requirement: [Source: _bmad-output/planning-artifacts/epics.md, Additional Requirements]
- Admin/support role and auditable sensitive actions: [Source: _bmad-output/planning-artifacts/prd.md, Domain-Specific Requirements; Admin, Moderation, and Support Operations]
- Trace/correlation and Problem Details conventions: [Source: _bmad-output/planning-artifacts/architecture.md, API and Communication Patterns; Process Patterns]
- Immutable auditability baseline: [Source: _bmad-output/planning-artifacts/architecture.md, Authentication and Security]
- Existing support timeline and moderation context: [Source: _bmad-output/implementation-artifacts/6-4-implement-event-timeline-and-correlation-based-troubleshooting.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 6.5 executed manually in Copilot chat (workflow CLI command not available in this shell)

### Completion Notes List

- Added immutable `PrivilegedActionAudits` entity/table, EF mapping, migration, and unique intent-key dedupe index for append-only audit writes.
- Introduced centralized privileged audit writer service and integrated moderation action outcomes (success, dedupe, forbidden, validation, confirmation, not-found, failed) with correlation and trace continuity.
- Added role-restricted privileged audit query endpoint under operations admin/support scope with bounded filters, deterministic ordering, pagination, and Problem Details validation.
- Extended support diagnostics UI to include privileged audit filters and read-only audit history table with trace/correlation and reason metadata.
- Added backend integration tests and frontend component tests covering privileged audit write/query behavior.

### File List

- _bmad-output/implementation-artifacts/6-5-implement-immutable-audit-logging-for-privileged-actions.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/PrivilegedActionAudit.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260430172025_AddPrivilegedActionAuditsStory65.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260430172025_AddPrivilegedActionAuditsStory65.Designer.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/TaskTrackerDbContextModelSnapshot.cs
- task-tracker-api/TaskTracker.Api/Features/Operations/Auditing/IPrivilegedAuditWriter.cs
- task-tracker-api/TaskTracker.Api/Features/Operations/Auditing/PrivilegedAuditWriter.cs
- task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/OperationsControllerTests.cs
- task-tracker-web/src/app/shared/models/support-diagnostics.models.ts
- task-tracker-web/src/app/shared/services/support-diagnostics.service.ts
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.ts
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.html
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.scss
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.spec.ts
