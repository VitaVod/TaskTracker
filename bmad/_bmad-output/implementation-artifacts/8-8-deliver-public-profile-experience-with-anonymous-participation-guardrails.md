# Story 8.8: Deliver Public Profile Experience with Anonymous Participation Guardrails

Status: done

## Story

As a user,
I want profile pages that respect visibility settings,
so that public stats are available only for opted-in participants.

## Acceptance Criteria

1. Given a public participant profile is requested, when page loads, then profile shows approved statistics and momentum highlights.
2. Given an anonymous participant profile is requested, when page loads, then statistics are not displayed and anonymous-participant message is shown.
3. Given leaderboard profile links are selected, when participant is public, then navigation resolves to profile page; otherwise anonymized behavior is preserved.
4. Given direct profile route access, when permission and visibility checks run, then backend and UI responses are privacy-safe and deterministic.

## Tasks / Subtasks

- [x] Add public profile read model and endpoint contracts (AC: 1, 4)
- [x] Implement anonymous participant response model/message behavior (AC: 2)
- [x] Link leaderboard identities to profile route when public (AC: 3)
- [x] Add tests for privacy-safe route access and response shape (AC: 2, 4)

## Dev Notes

- Enforce visibility rules server-side first, then mirror in UI.
- Keep profile payload limited to approved public fields.

### Project Structure Notes

- Leaderboard/profile API and read models: task-tracker-api/TaskTracker.Api
- Leaderboard/profile pages: task-tracker-web/src/app/features/leaderboard and profile

### Testing Requirements

- Integration tests for anonymous and public profile route behavior.
- UI tests verifying anonymous message rendering.

### References

- Source briefing: _bmad-output/planning-artifacts/bmad-briefing-2026-05-03.md
- Story inventory: _bmad-output/planning-artifacts/epics.md

## Dev Agent Record

### Completion Notes

- Added server-side public profile contracts/read models and endpoint behavior that only exposes approved fields for opted-in participants.
- Implemented deterministic anonymous fallback responses for invalid, non-public, or non-resolvable handles to prevent profile visibility leakage.
- Updated leaderboard payload and UI behavior so only public participants render navigable profile links; anonymous entries remain non-clickable.
- Added frontend public profile route/component with explicit loading/public/anonymous/error states and privacy-safe fallback handling.
- Bumped shared leaderboard cache schema version to invalidate stale payload shapes after contract changes.

### Test Evidence

- `dotnet test task-tracker-api/tests/TaskTracker.Api.Tests/TaskTracker.Api.Tests.csproj --no-restore` (pass, 176 total, 0 failed)
- `npx ng test --watch=false --browsers=ChromeHeadless --no-progress` (pass, 139 success)

## File List

- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Contracts/LeaderboardContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/ILeaderboardRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/LeaderboardRepository.cs
- task-tracker-api/TaskTracker.Api/Controllers/LeaderboardsController.cs
- task-tracker-api/TaskTracker.Api/Features/SharedViews/Caching/SharedViewCacheCoordinator.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/LeaderboardsControllerTests.cs
- task-tracker-web/src/app/shared/models/leaderboard.models.ts
- task-tracker-web/src/app/shared/services/leaderboard.service.ts
- task-tracker-web/src/app/shared/services/leaderboard.service.spec.ts
- task-tracker-web/src/app/features/leaderboards/leaderboard.component.ts
- task-tracker-web/src/app/features/leaderboards/leaderboard.component.html
- task-tracker-web/src/app/features/leaderboards/leaderboard.component.spec.ts
- task-tracker-web/src/app/features/leaderboards/public-profile.component.ts
- task-tracker-web/src/app/features/leaderboards/public-profile.component.html
- task-tracker-web/src/app/features/leaderboards/public-profile.component.scss
- task-tracker-web/src/app/features/leaderboards/public-profile.component.spec.ts
- task-tracker-web/src/app/features/leaderboards/index.ts
- task-tracker-web/src/app/app.routes.ts
- _bmad-output/implementation-artifacts/8-8-deliver-public-profile-experience-with-anonymous-participation-guardrails.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-05-03: Implemented privacy-safe public profile experience with deterministic anonymous guardrails across API and UI. Added integration/unit coverage and marked story done.
