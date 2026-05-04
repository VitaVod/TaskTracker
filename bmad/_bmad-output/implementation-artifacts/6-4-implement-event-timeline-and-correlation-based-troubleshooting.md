# Story 6.4: Implement Event Timeline and Correlation-Based Troubleshooting

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a support user,
I want an event timeline with trace context,
so that I can explain unexpected outcomes.

## Acceptance Criteria

1. Given a dispute scenario, when timeline query runs, then ordered events with timestamps, rule outcomes, and trace/correlation IDs are shown.
2. Given a dispute scenario, when timeline query runs, then timeline can be filtered by event type/date.

## Tasks / Subtasks

- [x] Implement support-only timeline query endpoint(s) with deterministic ordering (AC: 1, 2)
  - [x] Add authenticated support endpoint(s) that return timeline events for a target user with explicit support-role policy checks and standardized Problem Details responses.
  - [x] Support bounded filters (event type, start/end date window, max items) and reject invalid ranges with stable validation error codes.
  - [x] Enforce deterministic sort order (`occurredAtUtc` descending, then stable tie-break key) and include pagination metadata for safe large timelines.

- [x] Build correlation-aware timeline read model (AC: 1)
  - [x] Define timeline DTOs that include `eventId`, `eventType`, `occurredAtUtc`, `traceId`/correlation id, actor/target context, and rule outcome summary.
  - [x] Source events from existing progression/completion/moderation/support telemetry and audit stores; do not duplicate XP/streak rule execution.
  - [x] Normalize event rendering fields (human-readable message, machine-readable code, source subsystem) so support can explain outcomes consistently.

- [x] Extend support diagnostics UI with timeline and filters (AC: 1, 2)
  - [x] Add a timeline panel to the existing support diagnostics feature with event rows/cards showing timestamp, type, outcome, and correlation identifiers.
  - [x] Add filter controls for event type and date range with accessible keyboard/focus behavior and clear empty/error/loading states.
  - [x] Keep support UI read-only and avoid adding moderation or mutation controls.

- [x] Add observability and troubleshooting quality signals (AC: 1, 2)
  - [x] Emit structured logs for timeline requests with actor id/role, target user id, filter envelope, result count, and correlation id.
  - [x] Add metrics for timeline query latency, empty results, invalid filter attempts, and forbidden access attempts.
  - [x] Ensure API and logs preserve trace continuity with Story 6.1-6.3 suspicious-case and support diagnostic identifiers.

- [x] Add automated tests for timeline correctness, filtering, and role boundaries (AC: 1, 2)
  - [x] Backend integration tests for support success, non-support forbidden, invalid filter validation, and deterministic ordering guarantees.
  - [x] Backend tests for payload completeness (timestamps, rule outcomes, trace/correlation identifiers) and pagination behavior.
  - [x] Frontend tests for filter interactions, accessibility basics (focus/labels), and rendering of loading/empty/error/content states.

## Dev Notes

- Story 6.4 deepens support troubleshooting created in Story 6.3; it must remain read-only and explanation-oriented.
- Reuse correlation and suspicious-case identifiers from Stories 6.1 and 6.2 so timeline views align with moderation/audit evidence.
- Keep timeline contracts deterministic and bounded to prevent expensive unfiltered queries and inconsistent dispute explanations.
- Preserve centralized authorization and RFC 7807 Problem Details conventions for all validation/forbidden failures.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs`
  - `task-tracker-api/TaskTracker.Api/Authorization/`
  - `task-tracker-api/TaskTracker.Api/Features/Progress/`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/OperationsControllerTests.cs`

- Frontend likely touch points:
  - `task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.ts`
  - `task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.html`
  - `task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.scss`
  - `task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.spec.ts`
  - `task-tracker-web/src/app/shared/services/support-diagnostics.service.ts`
  - `task-tracker-web/src/app/shared/models/support-diagnostics.models.ts`

### Testing Requirements

- Verify support users can query timeline events for a target user and get deterministic event ordering with complete trace/correlation context.
- Verify non-support users are forbidden from timeline APIs and support timeline UI access.
- Verify event type/date filters behave deterministically and reject invalid ranges safely.
- Verify timeline payload includes explainability fields needed by support (timestamp, event type, rule outcome, trace/correlation ids).
- Verify UI filter controls and timeline rows are accessible and preserve read-only behavior.

### Previous Story Intelligence

- Story 6.3 delivered support read-only diagnostics endpoint and UI; Story 6.4 should extend this surface rather than introducing a parallel support feature area.
- Story 6.3 established observability shape (actor/target/correlation) and deterministic payload conventions; preserve these patterns for timeline APIs.
- Story 6.2 and 6.1 introduced moderation and suspicious-case correlation context that should appear in timeline events for end-to-end incident explanation.

### Git Intelligence Summary

- Recent implementation in this repo favors additive, low-refactor changes with explicit role checks, deterministic contracts, and integration test coverage.
- Keep changes contract-first and bounded to minimize regressions in progression, leaderboard, and operations workflows.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 6, Story 6.4]
- Support troubleshooting journey and event timeline expectations: [Source: _bmad-output/planning-artifacts/prd.md, Journey 4: Support User]
- Support role and privileged-action logging constraints: [Source: _bmad-output/planning-artifacts/prd.md, Domain-Specific Requirements]
- Role authorization, Problem Details, trace correlation, and auditability constraints: [Source: _bmad-output/planning-artifacts/architecture.md, Authentication and Security; API and Communication Patterns; Process Patterns]
- Existing support diagnostics baseline to extend: [Source: _bmad-output/implementation-artifacts/6-3-build-support-diagnostic-view-for-user-progress-disputes.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 6.4 executed manually in Copilot chat (workflow CLI command not available in this shell)

### Completion Notes List

- Added support timeline API endpoint at `/api/v1/ops/support/users/{userId}/timeline` with support-policy authorization, bounded filters (`eventType`, `startUtc`, `endUtc`, `page`, `maxItems`), deterministic ordering, and pagination metadata.
- Added correlation-aware timeline read model events sourced from completion events, XP ledger entries, moderation audits, and streak snapshots with explainability fields (message code, subsystem, actor/target context, rule outcome, trace/correlation identifiers).
- Added timeline observability with structured request logs and timeline metrics for success, empty results, forbidden access, invalid filters, and query latency.
- Extended support diagnostics UI/service/models to query and render timeline data with read-only event timeline panel and accessible event-type/date/max-item filter controls.
- Added backend integration tests and frontend component tests for timeline success, role boundaries, invalid date-range validation, deterministic ordering, and read-only timeline rendering states.
- Verified with:
  - `dotnet test TaskTracker.sln --no-restore --filter "FullyQualifiedName~OperationsControllerTests"`
  - `npx ng test --watch=false --browsers=ChromeHeadless --no-progress`

### File List

- _bmad-output/implementation-artifacts/6-4-implement-event-timeline-and-correlation-based-troubleshooting.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
- task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/OperationsControllerTests.cs
- task-tracker-web/src/app/shared/models/support-diagnostics.models.ts
- task-tracker-web/src/app/shared/services/support-diagnostics.service.ts
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.ts
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.html
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.scss
- task-tracker-web/src/app/features/support-diagnostics/support-diagnostics.component.spec.ts
