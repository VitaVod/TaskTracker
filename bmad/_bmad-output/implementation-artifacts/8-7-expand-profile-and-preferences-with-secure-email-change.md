# Story 8.7: Expand Profile and Preferences with Secure Email Change

Status: done

## Story

As a user,
I want clearer participation controls and secure email change,
so that privacy and account identity settings are easier to manage.

## Acceptance Criteria

1. Given profile preferences page, when leaderboard participation control renders, then control is visually clear and accessible.
2. Given user updates participation preference, when save succeeds, then leaderboard/public visibility behavior reflects latest setting.
3. Given authenticated user requests email change, when current password and new email are submitted, then verification flow is initiated and old email remains active until confirmation.
4. Given email confirmation is completed, when token is valid and unused, then account email is updated and old token is invalidated.

## Tasks / Subtasks

- [x] Redesign leaderboard participation control UI and helper text (AC: 1)
- [x] Wire preference updates to existing profile settings API (AC: 2)
- [x] Add secure email-change request endpoint with password re-authentication (AC: 3)
- [x] Add email-change confirmation endpoint with token checks (AC: 4)
- [x] Add tests for invalid token, expired token, and replay protection (AC: 4)

## Dev Notes

- Do not expose whether target email already exists in a way that leaks account enumeration data.
- Store token hashes, not raw tokens.

### Project Structure Notes

- Profile APIs and auth checks: task-tracker-api/TaskTracker.Api
- Profile preferences UI: task-tracker-web/src/app/features/profile

### Testing Requirements

- API tests for password confirmation and token replay prevention.
- UI tests for participation control accessibility.

### References

- Source briefing: _bmad-output/planning-artifacts/bmad-briefing-2026-05-03.md
- Story inventory: _bmad-output/planning-artifacts/epics.md

## Dev Agent Record

### Completion Notes

- Implemented accessible leaderboard participation controls as a radio group with clear helper copy and keyboard-focus support.
- Added secure email-change flow in account API:
	- request endpoint requiring password re-authentication;
	- confirmation endpoint validating token format, hash, expiry, and single-use consumption.
- Token handling uses hash-only persistence with constant-time comparison and replay protection via atomic token consume path (with in-memory EF fallback for tests).
- Account email remains unchanged until token confirmation succeeds.
- Added account-security notification events for email-change requested/completed dispatches.
- Added API integration tests for invalid token, expired token, replayed token, and successful confirmation semantics.
- Added account UI tests for participation control guidance and email-change request payload flow.
- Validation run results:
	- `dotnet test task-tracker-api/tests/TaskTracker.Api.Tests/TaskTracker.Api.Tests.csproj --no-restore` passed.
	- `npx ng test --watch=false --browsers=ChromeHeadless --no-progress` passed.

## File List

- task-tracker-api/TaskTracker.Api/Controllers/AccountController.cs
- task-tracker-api/TaskTracker.Api/Features/Account/Contracts/AccountContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Account/Repositories/IAccountRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Account/Repositories/AccountRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Email/ITransactionalEmailService.cs
- task-tracker-api/TaskTracker.Api/Features/Notifications/AccountEvents/IAccountEventNotificationService.cs
- task-tracker-api/TaskTracker.Api/Features/Notifications/AccountEvents/AccountEventNotificationService.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/AccountNotificationEventType.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/EmailChangeToken.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AccountControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-web/src/app/features/account/account-settings.component.html
- task-tracker-web/src/app/features/account/account-settings.component.scss
- task-tracker-web/src/app/features/account/account-settings.component.ts
- task-tracker-web/src/app/features/account/account-settings.component.spec.ts
- task-tracker-web/src/app/shared/services/account.service.ts

## Change Log

- 2026-05-03: Implemented secure account email-change request/confirm flow with hashed single-use tokens, added account notification events for email-change lifecycle, redesigned participation settings UX for accessibility, and added API/UI tests covering success and token invalidation scenarios.
