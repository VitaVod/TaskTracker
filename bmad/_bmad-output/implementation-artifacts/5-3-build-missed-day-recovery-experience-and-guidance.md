# Story 5.3: Build Missed-Day Recovery Experience and Guidance

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want a clear recovery path after missing a day,
so that I can re-engage quickly.

## Acceptance Criteria

1. Given a missed streak day is detected, when user returns to dashboard, then Recovery Prompt module explains impact and next-step action.
2. Given a missed streak day is detected, when user returns to dashboard, then messaging is supportive and deterministic.

## Tasks / Subtasks

- [x] Add deterministic missed-day recovery signal in progress contract (AC: 1, 2)
  - [x] Extend streak snapshot response under `/api/v1/progress/streak` with explicit recovery-oriented fields (for example, `isRecoveryPromptVisible`, `recoveryReason`, `recommendedAction`) derived from server-authoritative streak state.
  - [x] Keep API response and error contract conventions unchanged (RFC 7807 Problem Details with stable `code` and `traceId` for failures).
  - [x] Preserve UTC storage and timezone-projected day-boundary semantics when determining missed-day state.

- [x] Implement Recovery Prompt module in dashboard UI (AC: 1)
  - [x] Add a dashboard recovery prompt section that renders only when the server-reported recovery signal indicates missed-day recovery is needed.
  - [x] Show clear impact explanation and a concrete restart action (for example, navigate to task creation or active task list).
  - [x] Keep module responsive across mobile and desktop and aligned with existing dashboard card patterns.

- [x] Ensure supportive and deterministic message behavior (AC: 2)
  - [x] Use deterministic message mapping keyed by server-provided outcome/signal rather than ad-hoc client heuristics.
  - [x] Reuse existing progression language patterns already present in the app (for example, streak continuity/restart/reset guidance) instead of introducing conflicting phrasing.
  - [x] Ensure message copy is supportive, concise, and does not imply inconsistent XP/streak behavior.

- [x] Preserve accessibility and focus-flow behavior for recovery interactions (AC: 1, 2)
  - [x] Ensure keyboard navigation and focus order are deterministic when the recovery module appears.
  - [x] Add screen-reader friendly labels/announcements for recovery impact and next-step action.
  - [x] Keep recovery UI state compatible with existing loading/error/empty patterns on dashboard.

- [x] Add backend and frontend tests for recovery detection and presentation (AC: 1, 2)
  - [x] Backend tests proving missed-day detection and recovery signal mapping are deterministic across timezone boundaries.
  - [x] Frontend component tests for conditional rendering, deterministic messaging, and action CTA behavior.
  - [x] Regression tests ensuring non-missed-day users do not see the recovery prompt and existing dashboard cards continue to render correctly.

## Dev Notes

- Story 5.3 should build on existing progression and dashboard surfaces rather than introducing a parallel progress/read model. Current streak data already flows through `ProgressController` and `ProgressService`; extend these contracts incrementally.
- Keep server-state authoritative for recovery messaging triggers. Avoid computing streak/missed-day logic exclusively in Angular when backend has the source-of-truth time semantics.
- The UX requirement is a Recovery Prompt module with clear impact + next action. Implement it as an additive dashboard module that does not regress momentum summary, global statistics, or existing progress cards.
- Ensure deterministic behavior around timezone/day-boundary handling consistent with existing progression architecture.

### Project Structure Notes

- Backend likely touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/ProgressController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Progress/Contracts/ProgressContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Progress/Repositories/IProgressRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Progress/Repositories/ProgressRepository.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/ProgressControllerTests.cs`

- Frontend likely touch points:
  - `task-tracker-web/src/app/features/dashboard/dashboard.component.ts`
  - `task-tracker-web/src/app/features/dashboard/dashboard.component.spec.ts`
  - `task-tracker-web/src/app/shared/models/progress.models.ts`
  - `task-tracker-web/src/app/shared/services/progress.service.ts`
  - Optional shared UX extraction if needed for reuse:
    - `task-tracker-web/src/app/shared/` (only if deterministic copy mapping is shared across features)

### Testing Requirements

- Verify missed-day recovery signal is produced deterministically from streak state/timezone-aware day boundaries.
- Verify dashboard shows Recovery Prompt only for missed-day scenarios and hides it otherwise.
- Verify recovery prompt includes both impact explanation and next-step action.
- Verify copy remains deterministic and supportive across `continue`/`restart`/`reset`-related paths.
- Verify keyboard navigation and accessible labels/announcements for recovery prompt module.
- Verify existing dashboard progress, momentum, and statistics cards still pass current behavior tests.

### Previous Story Intelligence

- Story 5.2 established deterministic reminder scheduling and reuse-first behavior over existing notification/auth abstractions; follow the same additive approach here (no parallel progression stack).
- Existing UI already contains deterministic streak guidance patterns in task-list progress feedback. Reuse or align wording/logic to avoid conflicting guidance between task and dashboard surfaces.
- Prior stories in epics 3 and 4 emphasized server-confirmed progression state and deterministic user messaging; this story should preserve that trust model.

### Git Intelligence Summary

- Recent commits are large but trend toward additive feature delivery with deterministic behavior hardening and tests.
- Story 5.3 should remain scoped: add recovery prompt capability and tests without refactoring unrelated auth/leaderboard/reminder flows.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 5, Story 5.3]
- Functional requirement mapping (`FR39`, `FR40`, `FR41`, `FR42`) and UX requirement `UX-DR8`: [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory; UX Design Requirements]
- Product recovery and deterministic time-semantics expectations: [Source: _bmad-output/planning-artifacts/prd.md, Engagement, Recovery, and Product Guidance; Reliability; Timezone and daily-boundary constraints]
- UX flow and component target for missed-day recovery: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Missed-Day Recovery Flow; Custom Components]
- Architecture constraints for server-authoritative progression, Problem Details, UTC/timezone policy: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; Format Patterns; Process Patterns]
- Existing implementation touchpoints: [Source: task-tracker-api/TaskTracker.Api/Controllers/ProgressController.cs; task-tracker-api/TaskTracker.Api/Features/Progress/Repositories/ProgressRepository.cs; task-tracker-web/src/app/features/dashboard/dashboard.component.ts]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed manually (BMAD CLI command not available in this shell)

### Completion Notes List

- Extended `GET /api/v1/progress/streak` response with deterministic recovery signal fields: `isRecoveryPromptVisible`, `recoveryReason`, and `recommendedAction`.
- Added server-authoritative missed-day/restart mapping in `ProgressRepository` using timezone-projected local-day gap logic.
- Added dashboard Recovery Prompt module with deterministic copy/action mapping and accessibility-friendly live announcement text.
- Added backend integration tests and frontend component tests covering prompt visibility, reason/action mapping, and non-visible scenarios.

### File List

- task-tracker-api/TaskTracker.Api/Features/Progress/Contracts/ProgressContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Progress/Repositories/IProgressRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Progress/Repositories/ProgressRepository.cs
- task-tracker-api/TaskTracker.Api/Controllers/ProgressController.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/ProgressControllerTests.cs
- task-tracker-web/src/app/shared/models/progress.models.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.spec.ts
- task-tracker-web/src/app/shared/services/progress.service.spec.ts
- task-tracker-web/src/app/features/tasks/task-list.component.spec.ts
- _bmad-output/implementation-artifacts/5-3-build-missed-day-recovery-experience-and-guidance.md