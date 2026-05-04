# Story 8.4: Add Weekly Recovery Token and Near-Miss Streak Nudges

Status: done

## Story

As a user,
I want occasional streak protection and timely nudges,
so that a single missed day does not fully break momentum.

## Acceptance Criteria

1. Given streak evaluation in user timezone, when a missed day occurs and weekly token is available, then one recovery token is consumed and streak continuity is preserved.
2. Given token lifecycle events, when token is granted or consumed, then auditable records exist with local date context.
3. Given user is one task short of preserving streak tier, when nudge window is reached and preferences allow, then near-miss reminder is sent at most once per local day.
4. Given user notification preferences disable reminders, when near-miss condition occurs, then no nudge is sent.

## Tasks / Subtasks

- [x] Implement recovery token grant/consume lifecycle (AC: 1, 2)
- [x] Integrate token logic with timezone-aware streak engine (AC: 1)
- [x] Implement near-miss detection and one-per-day dispatch guard (AC: 3)
- [x] Respect notification preferences and quiet hours (AC: 4)
- [x] Add tests for timezone boundary and duplicate-send prevention (AC: 1, 3)

## Dev Notes

- Use persisted local-date projections for deterministic streak decisions.
- Keep nudge scheduling idempotent with unique daily dispatch keys.

### Project Structure Notes

- Streak engine and jobs: task-tracker-api/TaskTracker.Api
- Notification preferences and templates: task-tracker-api/TaskTracker.Api

### Testing Requirements

- Test missed-day token consumption across timezone boundaries.
- Test no duplicate nudge for same user/day.

### References

- Source briefing: _bmad-output/planning-artifacts/bmad-briefing-2026-05-03.md
- Story inventory: _bmad-output/planning-artifacts/epics.md

## Dev Agent Record

### Completion Notes

- Added weekly recovery token lifecycle to streak processing with deterministic local-week keys and persisted token balance on streak snapshots.
- Extended streak evaluation logic to consume one token for exactly one missed local day and preserve continuity for that evaluation.
- Added auditable token lifecycle events with local date and week context via `StreakRecoveryTokenEvents` persistence.
- Updated reminder processing to near-miss behavior: requires streak tier risk state, local nudge window, no quiet-hours overlap, and per-local-day dedupe using local-day UTC windows.
- Preserved reminder preference behavior by continuing to gate dispatches on `ReminderEmailEnabled`.

### Test Evidence

- `dotnet test task-tracker-api\\tests\\TaskTracker.Api.Tests\\TaskTracker.Api.Tests.csproj --no-restore` (pass)
- `dotnet test TaskTracker.sln --no-restore` (pass)
- `npx ng test --watch=false --browsers=ChromeHeadless --no-progress` (pass)

## File List

- task-tracker-api/TaskTracker.Api/Features/Tasks/Streaks/StreakRuleEngine.cs
- task-tracker-api/TaskTracker.Api/Features/Tasks/Repositories/TaskRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Notifications/Reminders/ReminderProcessingService.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/UserStreakSnapshot.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/StreakRecoveryTokenEvent.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/StreakRecoveryTokenEventType.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260503141547_AddWeeklyRecoveryTokenAndNearMissNudgesStory84.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260503141547_AddWeeklyRecoveryTokenAndNearMissNudgesStory84.Designer.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/TaskTrackerDbContextModelSnapshot.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Unit/StreakRuleEngineTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/TasksControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/NotificationRemindersControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- _bmad-output/implementation-artifacts/sprint-status.yaml
- _bmad-output/implementation-artifacts/8-4-add-weekly-recovery-token-and-near-miss-streak-nudges.md

## Change Log

- 2026-05-03: Implemented weekly recovery token lifecycle, token consumption during streak evaluation, near-miss nudge gating and local-day dedupe, plus unit/integration coverage for token and nudge paths.
- 2026-05-03: Added EF Core migration `AddWeeklyRecoveryTokenAndNearMissNudgesStory84` and moved story status to done.
