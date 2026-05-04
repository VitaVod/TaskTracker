# Story 5.2: Implement Reminder Email Pipeline for Pending Tasks

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want reminder emails for pending or incomplete tasks,
so that I stay on track.

## Acceptance Criteria

1. Given reminder job execution and eligible pending tasks, when reminder processing runs, then emails are sent according to user preferences and schedule rules.
2. Given reminder job execution and eligible pending tasks, when reminder processing runs, then delivery failures are retried and logged.

## Tasks / Subtasks

- [x] Add reminder-processing application flow that finds eligible users/tasks (AC: 1)
  - [x] Add a notifications/reminders feature flow in backend code that loads users with reminders enabled and resolves their pending/incomplete tasks.
  - [x] Ensure ownership-safe task filtering (only tasks belonging to each target user are included in that user's reminder payload).
  - [x] Keep read/query behavior deterministic (stable ordering and bounded selection per run) to avoid duplicate or drifting payloads.

- [x] Enforce preference-aware cadence and schedule rules (AC: 1)
  - [x] Use Story 5.1 preference fields (`ReminderEmailEnabled`, `ReminderCadence`) as the only source of truth for reminder eligibility.
  - [x] Implement cadence checks (daily/weekly) using UTC timestamps and documented schedule-window policy.
  - [x] Prevent duplicate reminders in the same cadence window by storing delivery attempt metadata or equivalent dedupe state.

- [x] Integrate reminder email delivery contract with retry semantics (AC: 1, 2)
  - [x] Extend or add email notification abstraction for reminder messages without breaking existing password-recovery behavior.
  - [x] Implement transient-failure retry strategy consistent with Story 1.6 approach (retry transient infrastructure failures, do not retry permanent/business failures).
  - [x] Ensure each attempt captures trace correlation and structured result status for operations visibility.

- [x] Add execution trigger for reminder job processing (AC: 1, 2)
  - [x] Add a background hosted service and/or explicit internal trigger endpoint aligned with current architecture patterns and security policies.
  - [x] Ensure trigger execution is safe to run repeatedly and does not create duplicate sends for the same cadence window.
  - [x] Add clear run-level logging (started, completed, skipped, failed counts).

- [x] Add tests for eligibility, retry, and logging behavior (AC: 1, 2)
  - [x] Integration tests for preference-enabled users receiving reminders and preference-disabled users being skipped.
  - [x] Integration tests for cadence-window enforcement (daily/weekly) and dedupe behavior.
  - [x] Tests for transient email failures proving retry attempts and final outcome handling.
  - [x] Tests that verify structured logging and traceable failure records for operational troubleshooting.

## Dev Notes

- Story 5.2 should build directly on Story 5.1 preference persistence/API and Story 1.6 email retry semantics. Do not introduce parallel preference stores or unrelated email abstractions.
- Existing codebase currently keeps feature logic under `TaskTracker.Api` (controllers, repositories, validators). Follow the established structure in this repository rather than introducing a large architecture refactor in this story.
- Reminder processing must remain deterministic and idempotency-aware at cadence-window level so repeated job runs do not spam users.
- Time handling should remain UTC-based for storage/processing, matching broader project time policy.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/` (if adding internal trigger endpoint)
  - `task-tracker-api/TaskTracker.Api/Features/Notifications/` (contracts/validation/reminder orchestration)
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Email/` (shared transactional email adapter extensions)
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/` (pending/incomplete task query reuse or extension)
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/` (if tracking reminder-run metadata)
  - `task-tracker-api/TaskTracker.Api/Program.cs` (DI and hosted service wiring)
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`

- Frontend impact in this story should be minimal/non-required unless an ops-safe trigger or status surface is explicitly scoped into UI.

### Testing Requirements

- Verify reminder processing respects `ReminderEmailEnabled` and `ReminderCadence` values from persisted user preferences.
- Verify only pending/incomplete tasks are included in reminders; completed tasks are excluded.
- Verify cadence-window dedupe prevents duplicate reminders for repeated runs in the same window.
- Verify transient email failures are retried and permanent failures are logged without infinite retries.
- Verify logs include trace context and enough metadata to investigate failed runs.

### Previous Story Intelligence

- Story 5.1 already established preference defaults and validation (`daily`, reminders enabled by default) and exposed authenticated endpoints under `/api/v1/notifications/preferences`.
- Story 5.1 implemented deterministic update semantics and Problem Details conventions that this story should preserve when introducing any trigger endpoint or error response.
- Story 1.6 established transactional email retry semantics and test patterns (`ITransactionalEmailService`, fake email service behavior in integration tests) that should be reused for reminders.

### Git Intelligence Summary

- Recent commits show additive, story-scoped implementation with strong test coverage and deterministic behavior hardening.
- Keep Story 5.2 incremental: add reminder pipeline behavior and tests without refactoring unrelated auth/task/leaderboard modules.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 5, Story 5.2]
- Requirements context (`FR45`, `FR47`, and dependency context `FR46`, `FR48`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory]
- Notifications/reminders and reliability requirements: [Source: _bmad-output/planning-artifacts/prd.md, Notifications and Reminders; Non-Functional Requirements - Reliability]
- API, error, and process patterns (Problem Details, retry strategy, UTC payload expectations): [Source: _bmad-output/planning-artifacts/architecture.md, Format Patterns; Process Patterns; Structure Patterns]
- Preference baseline from previous story: [Source: _bmad-output/implementation-artifacts/5-1-implement-notification-preferences-domain-and-api.md]
- Existing backend baseline files: [Source: task-tracker-api/TaskTracker.Api/Controllers/NotificationPreferencesController.cs; task-tracker-api/TaskTracker.Api/Features/Auth/Email/ITransactionalEmailService.cs; task-tracker-api/TaskTracker.Api/Program.cs]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed manually (BMAD CLI command not available in this shell)

### Completion Notes List

- Added reminder processing service with deterministic user/task selection, cadence windows, retry semantics, and run-level logging.
- Added internal admin trigger endpoint (`POST /api/v1/internal/notifications/reminders/run`) to execute reminder processing safely on-demand.
- Added reminder dispatch persistence metadata for cadence-window dedupe and operational traceability.
- Added EF Core migration to create reminder dispatch persistence schema for SQL Server environments.
- Added Story 5.2 integration tests for eligibility, dedupe, retry, failure logging, and authorization behavior.

### File List

- task-tracker-api/TaskTracker.Api/Controllers/NotificationRemindersController.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Email/ITransactionalEmailService.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Email/LoggingTransactionalEmailService.cs
- task-tracker-api/TaskTracker.Api/Features/Notifications/Contracts/ReminderProcessingContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Notifications/Reminders/IReminderProcessingService.cs
- task-tracker-api/TaskTracker.Api/Features/Notifications/Reminders/ReminderProcessingService.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/NotificationReminderDispatch.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/NotificationReminderDispatchStatus.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260430125321_AddReminderDispatchPipelineStory52.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260430125321_AddReminderDispatchPipelineStory52.Designer.cs
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/NotificationRemindersControllerTests.cs