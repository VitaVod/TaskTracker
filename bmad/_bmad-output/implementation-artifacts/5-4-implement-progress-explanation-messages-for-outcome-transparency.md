# Story 5.4: Implement Progress Explanation Messages for Outcome Transparency

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to understand why my streak or XP changed,
so that I trust system behavior.

## Acceptance Criteria

1. Given completion and streak events, when outcome explanation is requested or displayed, then clear reason text links action to resulting XP/streak state.
2. Given completion and streak events, when outcome explanation is requested or displayed, then explanations align exactly with backend rules.

## Tasks / Subtasks

- [x] Add server-authoritative progress explanation fields to progress contracts (AC: 1, 2)
  - [x] Extend progress/streak API response contracts with explicit explanation metadata for XP and streak outcome reasons (for example, reason codes plus rendered explanation text source fields).
  - [x] Keep error contract conventions unchanged (RFC 7807 Problem Details with stable `code` and `traceId` fields).
  - [x] Preserve deterministic timezone/day-boundary semantics from backend as the source-of-truth.

- [x] Implement deterministic explanation mapping in backend progression logic (AC: 1, 2)
  - [x] Add explanation mapping from completion/streak outcomes to user-facing reason messages in progression services/read-model assembly.
  - [x] Ensure mapping is deterministic and tied directly to applied backend rules (no heuristic-only client interpretation).
  - [x] Reuse established outcome categories from existing progression and recovery logic where applicable.

- [x] Surface explanation messages in dashboard and relevant progress UI touchpoints (AC: 1)
  - [x] Add explanation display in progress modules where XP/streak outcomes are shown.
  - [x] Ensure explanation copy clearly links user action to resulting outcome in concise language.
  - [x] Keep rendering responsive and consistent with existing dashboard/task feedback card patterns.

- [x] Ensure accessibility and predictable interaction behavior for explanation content (AC: 1)
  - [x] Provide semantic markup/labels so screen readers can interpret outcome explanation text correctly.
  - [x] Keep focus order and keyboard navigation deterministic when explanation content appears or updates.
  - [x] Ensure explanation content behaves correctly in loading, empty, and error states.

- [x] Add backend and frontend tests for explanation correctness and deterministic behavior (AC: 1, 2)
  - [x] Backend tests covering explanation mapping for key scenarios (streak continuation, streak break/recovery, XP award outcomes, duplicate/idempotent events).
  - [x] Frontend component tests for conditional rendering and exact message mapping to server-provided outcome metadata.
  - [x] Regression tests proving displayed explanations remain aligned with backend rules and do not diverge across timezone boundaries.

## Dev Notes

- Story 5.4 builds directly on existing progression and recovery work from Epic 3 and Story 5.3. Keep backend as the authoritative source for outcome reason semantics.
- Avoid introducing a parallel explanation engine in Angular. Client should render backend-provided explanation signals/text or deterministic mappings keyed to backend reason codes.
- Preserve existing progress API conventions and avoid breaking current dashboard consumers.
- Keep explanation language supportive, deterministic, and consistent with existing progression/recovery wording.

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

### Testing Requirements

- Verify explanation text deterministically reflects backend-applied streak/XP rules.
- Verify explanation messages appear only when requested/displayed by product flow and remain clear and concise.
- Verify explanation behavior across timezone/day-boundary transitions is consistent with server-authoritative rule outcomes.
- Verify existing dashboard progress and recovery components continue to pass current behavior tests.

### Previous Story Intelligence

- Story 5.3 introduced recovery prompts and deterministic outcome guidance; reuse its server-authoritative signaling approach and language consistency.
- Story 5.2 established deterministic and preference-aware notification/retry patterns; keep the same additive, low-regression implementation style.
- Epic 3 progression stories emphasized idempotent and explainable progress processing; this story should preserve that trust model end-to-end.

### Git Intelligence Summary

- Existing implementation artifacts show additive story execution and deterministic behavior hardening.
- Scope for 5.4 should remain focused on explanation correctness and UX transparency, without refactoring unrelated task, auth, or leaderboard features.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 5, Story 5.4]
- Functional requirement mapping (`FR40`, `FR41`) and recovery/transparency context: [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory]
- Product expectations for trust, deterministic progression feedback, and transparency: [Source: _bmad-output/planning-artifacts/prd.md, Engagement/Progression experience]
- UX flows and motivational feedback guidance: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Core User Experience; Emotional Design Principles]
- Architecture constraints for server-authoritative progression, Problem Details, UTC/timezone policy: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; API and Communication Patterns]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story 5.4 executed manually (workflow CLI command not available in this shell)

### Completion Notes List

- Added server-authoritative explanation metadata to progress contracts for XP and streak outcomes using deterministic `reasonCode` + `message` fields.
- Implemented backend explanation mapping in the progress repository for XP/no-XP states, streak outcome states, and recovery visibility states.
- Updated dashboard rendering to consume and display backend-provided explanation text directly (including recovery explanation content) without client-side reason-text derivation.
- Extended integration and component/service tests to validate explanation fields and rendering behavior.
- Validation executed:
  - `dotnet test .\\tests\\TaskTracker.Api.Tests\\TaskTracker.Api.Tests.csproj --filter "ProgressControllerTests"` (8/8 passed)
  - `npx ng test --watch=false --browsers=ChromeHeadless --include="src/app/features/dashboard/dashboard.component.spec.ts" --include="src/app/shared/services/progress.service.spec.ts" --include="src/app/features/tasks/task-list.component.spec.ts"` (36/36 passed)

### File List

- _bmad-output/implementation-artifacts/5-4-implement-progress-explanation-messages-for-outcome-transparency.md
- task-tracker-api/TaskTracker.Api/Features/Progress/Contracts/ProgressContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Progress/Repositories/IProgressRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Progress/Repositories/ProgressRepository.cs
- task-tracker-api/TaskTracker.Api/Controllers/ProgressController.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/ProgressControllerTests.cs
- task-tracker-web/src/app/shared/models/progress.models.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.spec.ts
- task-tracker-web/src/app/features/tasks/task-list.component.spec.ts
- task-tracker-web/src/app/shared/services/progress.service.spec.ts
