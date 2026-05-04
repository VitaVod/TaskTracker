# Story 8.9: Harden Transactional Email Deliverability and Recovery Flows

Status: done

## Story

As a platform operator,
I want reliable recovery and notification delivery,
so that users consistently receive security-critical and reminder emails.

## Acceptance Criteria

1. Given transactional email sends, when provider accepts or rejects messages, then delivery status and provider message identifiers are logged.
2. Given transient provider failures, when retry policy applies, then retries are bounded and observable.
3. Given persistent delivery failure, when user initiates recovery, then user receives actionable guidance and support-safe error messaging.
4. Given environment is misconfigured, when startup health checks run, then mail configuration issues are surfaced to operations.

## Tasks / Subtasks

- [x] Add structured delivery logging and provider correlation fields (AC: 1)
- [x] Implement bounded retry policies with clear terminal failure state (AC: 2)
- [x] Improve recovery-flow user messaging for send failures (AC: 3)
- [x] Add email configuration health-check endpoint/diagnostics (AC: 4)
- [x] Add integration tests with provider test doubles and failure modes (AC: 1, 2, 3)

## Dev Notes

- Never log secrets or full tokens.
- Keep delivery diagnostics queryable by trace/correlation ID.

### Project Structure Notes

- Email pipeline services: task-tracker-api/TaskTracker.Api
- Recovery and notification tests: task-tracker-api/tests/TaskTracker.Api.Tests

### Testing Requirements

- Simulate accepted, transient failure, and permanent failure outcomes.
- Verify retry ceiling and final status persistence.

### References

- Source briefing: _bmad-output/planning-artifacts/bmad-briefing-2026-05-03.md
- Story inventory: _bmad-output/planning-artifacts/epics.md

## Dev Agent Record

### Debug Log

- Updated transactional email send contract to include provider outcome metadata.
- Added structured provider response logging to account event and reminder delivery pipelines.
- Added email configuration health check and surfaced diagnostics via admin health endpoint.
- Updated password recovery request response with support-safe guidance for delivery failures.
- Extended integration tests and fake provider test double to cover provider correlation data and health diagnostics payload.

### Completion Notes

- AC1 satisfied via structured logs that now include provider status, provider message ID, and provider error code across success and failure paths.
- AC2 retained and validated through existing bounded retry logic with enhanced observability per attempt and terminal outcomes.
- AC3 satisfied by returning actionable, support-safe recovery guidance in password recovery request responses without revealing account existence.
- AC4 satisfied by startup-run health checks and admin endpoint diagnostics for email configuration validity.
- Validation: `dotnet test TaskTracker.sln --no-restore` passed with 177/177 tests.

## File List

- task-tracker-api/TaskTracker.Api/Features/Auth/Email/ITransactionalEmailService.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Email/LoggingTransactionalEmailService.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Email/EmailConfigurationHealthCheck.cs
- task-tracker-api/TaskTracker.Api/Features/Notifications/AccountEvents/AccountEventNotificationService.cs
- task-tracker-api/TaskTracker.Api/Features/Notifications/Reminders/ReminderProcessingService.cs
- task-tracker-api/TaskTracker.Api/Controllers/AuthController.cs
- task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs

## Change Log

- 2026-05-03: Implemented provider-aware delivery outcomes, structured retry observability logs, recovery guidance messaging, and email configuration health diagnostics with integration test coverage.
