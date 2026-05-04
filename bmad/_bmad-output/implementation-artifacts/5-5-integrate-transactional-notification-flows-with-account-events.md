# Story 5.5: Integrate Transactional Notification Flows with Account Events

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want critical account event notifications delivered reliably,
so that I can act on security-related events quickly.

## Acceptance Criteria

1. Given account-critical events (password reset, security-related account actions), when transactional pipeline executes, then required emails are sent with monitored status and retry behavior.
2. Given account-critical events (password reset, security-related account actions), when transactional pipeline executes, then failures surface to operational logs/alerts.

## Tasks / Subtasks

- [x] Extend transactional notification event coverage for account-critical events (AC: 1)
  - [x] Identify account-critical event set and map each event to required transactional email templates/payloads.
  - [x] Ensure existing password recovery notifications remain backward-compatible and unchanged for current clients.
  - [x] Keep all account-notification routing preference-aware based on existing account notification settings.

- [x] Implement reliable delivery orchestration with monitored status lifecycle (AC: 1, 2)
  - [x] Reuse existing transactional email abstraction to dispatch account-event emails through a single delivery pipeline.
  - [x] Capture deterministic attempt lifecycle metadata (queued, processing, succeeded, failed-transient, failed-permanent) for each account event notification.
  - [x] Ensure trace correlation is attached to each delivery attempt for troubleshooting.

- [x] Add retry behavior for transient failures and stable failure handling for permanent errors (AC: 1, 2)
  - [x] Apply bounded retry policy for transient provider/infrastructure failures.
  - [x] Do not retry permanent/business-rule failures; record final outcome explicitly.
  - [x] Prevent duplicate sends for the same account event occurrence via idempotent event/delivery keys.

- [x] Surface operational observability for transactional account notifications (AC: 2)
  - [x] Add structured logs for event receipt, send attempts, retries, and terminal outcomes.
  - [x] Expose failure signals to existing operations alerting path or internal diagnostics endpoint used by this project.
  - [x] Include minimal PII in logs while preserving actionable diagnostics (event type, user id, correlation id, failure category).

- [x] Add backend and integration tests for account-event delivery reliability (AC: 1, 2)
  - [x] Integration tests proving required account-event emails are triggered for critical flows.
  - [x] Tests proving transient failures are retried and terminal outcomes are recorded.
  - [x] Tests proving permanent failures are not retried and are surfaced to operational logging/alert hooks.
  - [x] Regression tests ensuring reminder pipeline (Story 5.2) and password recovery baseline (Story 1.6) continue to pass.

## Dev Notes

- Story 5.5 should integrate with existing notifications/auth infrastructure from Stories 1.6, 5.1, and 5.2 rather than introducing a separate parallel email pipeline.
- Keep delivery behavior deterministic and idempotent for repeated event processing.
- Preserve Problem Details conventions and current API contracts unless a contract extension is explicitly required.
- Ensure any new operational logging follows current repository conventions for trace and correlation identifiers.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/AuthController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Email/ITransactionalEmailService.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Email/LoggingTransactionalEmailService.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Notifications/Reminders/ReminderProcessingService.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/`
  - `task-tracker-api/TaskTracker.Api/Program.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`

- Frontend impact is expected to be minimal for this story unless account-event notification state is intentionally surfaced in user settings or support diagnostics UI.

### Testing Requirements

- Verify transactional emails are produced for each defined critical account event path.
- Verify retry behavior only applies to transient failures and remains bounded.
- Verify terminal failures are observable in structured logs/alerts with correlation identifiers.
- Verify no duplicate emails for repeated processing of the same account event.
- Verify existing reminder and password recovery test suites remain green.

### Previous Story Intelligence

- Story 1.6 established password recovery email flow and retry semantics via transactional email service.
- Story 5.1 introduced notification preferences including account notification enablement controls.
- Story 5.2 introduced reminder delivery orchestration, dispatch status persistence, and operational logging patterns that should be reused for consistency.

### Git Intelligence Summary

- Recent work favors additive, deterministic behavior changes with integration tests.
- Scope for 5.5 should stay focused on account-event notification reliability and observability without refactoring unrelated progression or leaderboard modules.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 5, Story 5.5]
- Functional requirement mapping (`FR48`) and Epic 5 context (`FR45`, `FR46`, `FR47`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory]
- Product reliability and account notification expectations: [Source: _bmad-output/planning-artifacts/prd.md]
- Architecture constraints for Problem Details, deterministic processing, and operational observability: [Source: _bmad-output/planning-artifacts/architecture.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 5.5 executed manually (workflow CLI command not available in this shell)

### Completion Notes List

- Added `AccountEventNotificationService` pipeline to process account-critical email events through deterministic dispatch lifecycle states (`queued`, `processing`, `succeeded`, `failed_transient`, `failed_permanent`) with bounded retries and idempotent event keys.
- Integrated password recovery request and password reset confirmation flows with account-event delivery orchestration while preserving existing password recovery API behavior.
- Ensured account-event dispatch respects `AccountEmailEnabled` user preferences and records attempt metadata/correlation for troubleshooting.
- Added admin diagnostics endpoint for failed account notifications: `GET /api/v1/ops/admin/account-notifications/failures`.
- Expanded integration tests for preference-aware routing, account-event send coverage, permanent-failure diagnostics visibility, and regression execution of existing auth/reminder suites.
- Executed `dotnet test` in `task-tracker-api/tests/TaskTracker.Api.Tests`: 115 passed, 0 failed.

### File List

- _bmad-output/implementation-artifacts/5-5-integrate-transactional-notification-flows-with-account-events.md
- task-tracker-api/TaskTracker.Api/Controllers/AuthController.cs
- task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Email/ITransactionalEmailService.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Email/LoggingTransactionalEmailService.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Repositories/IAuthRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Repositories/AuthRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Notifications/AccountEvents/IAccountEventNotificationService.cs
- task-tracker-api/TaskTracker.Api/Features/Notifications/AccountEvents/AccountEventNotificationService.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/AccountNotificationDispatch.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/AccountNotificationDispatchStatus.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/AccountNotificationEventType.cs
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
