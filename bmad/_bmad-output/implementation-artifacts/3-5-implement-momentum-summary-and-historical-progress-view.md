# Story 3.5: Implement Momentum Summary and Historical Progress View

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to see daily/weekly completion trend context,
so that I stay motivated beyond individual task events.

## Acceptance Criteria

1. Given historical completion data exists, when momentum view loads, then cumulative and recent trend metrics are displayed clearly.
2. Given momentum indicators are rendered, when users rely on non-visual or low-vision cues, then visual indicators do not rely on color alone.

## Tasks / Subtasks

- [x] Define momentum-view contract mapping from existing progress APIs (AC: 1)
  - [x] Reuse `ProgressTrendSummary`, `ProgressTrendPoint`, `ProgressXpSummary`, and `ProgressStreakSnapshot` models already exposed by Story 3.3.
  - [x] Standardize dashboard view-model fields for daily and weekly trend summaries without duplicating server-side progression logic.
  - [x] Keep API payload interpretation deterministic for repeated requests and partial-data conditions.

- [x] Build Momentum Summary dashboard block for cumulative and recent metrics (AC: 1)
  - [x] Add cumulative metrics (for example total XP, current streak, longest streak, total completed in selected window) with clear labels.
  - [x] Add recent-trend metrics (for example last 7-day completion and week-over-week delta) sourced from trend snapshot data.
  - [x] Preserve explicit loading, empty, and error states with actionable recovery text.

- [x] Add daily and weekly historical progress views with bounded windows (AC: 1)
  - [x] Use `ProgressService.getTrendSummary(granularity, windowDays)` for both daily and weekly views.
  - [x] Keep the default time window bounded (for example 30 days daily, 12 weeks weekly) to align with API bounded-latency intent.
  - [x] Ensure user-selected granularity and window changes do not trigger conflicting concurrent state updates.

- [x] Implement non-color-only momentum indicators and accessibility semantics (AC: 2)
  - [x] Pair directional colors with explicit text/icon state (up, down, unchanged) so trend meaning remains clear without color cues.
  - [x] Provide assistive labels and/or table summaries that communicate trend outcomes in text.
  - [x] Validate keyboard navigation order and focus management for granularity/window controls.

- [x] Integrate momentum view into existing dashboard information architecture (AC: 1, 2)
  - [x] Extend current dashboard component composition rather than introducing duplicate progress pages.
  - [x] Keep responsive behavior legible at mobile and desktop breakpoints with chart/list fallback when space is constrained.
  - [x] Respect reduced-motion preferences for trend transitions.

- [x] Add tests for trend rendering determinism, accessibility, and state handling (AC: 1, 2)
  - [x] Unit/component tests for daily/weekly trend mapping and metric-card rendering.
  - [x] Tests for empty-history and error branches that assert clear user guidance.
  - [x] Accessibility checks verifying non-color-only indicators and assistive labeling.

## Dev Notes

- Story 3.5 builds directly on Story 3.3 progress endpoints and Story 3.4 dashboard patterns; avoid introducing alternate progression calculations in frontend code.
- Keep server state authoritative for XP/streak/trend values. The frontend should transform data for presentation only.
- Maintain `/api/v1` contract usage through existing `ProgressService`; do not add ad-hoc endpoints unless acceptance criteria cannot be met.
- Momentum UX must keep motivation clear and trustworthy: trend insights should be understandable at a glance and remain interpretable with assistive technologies.
- Preserve deterministic rendering under retries/reloads so the same API snapshot yields the same visual and textual outcomes.

### Project Structure Notes

- Frontend expected touch points:
  - `task-tracker-web/src/app/features/dashboard/dashboard.component.ts`
  - `task-tracker-web/src/app/features/dashboard/dashboard.component.spec.ts`
  - `task-tracker-web/src/app/shared/services/progress.service.ts`
  - `task-tracker-web/src/app/shared/models/progress.models.ts`

- Optional backend verification touch points (only if API contract gaps are discovered):
  - `task-tracker-api/TaskTracker.Api/Controllers/ProgressController.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/ProgressControllerTests.cs`

- Follow existing feature-first Angular structure and reuse established progress-service patterns from Epic 3.

### Testing Requirements

- Verify momentum summary renders cumulative and recent trend metrics from authenticated progress APIs.
- Verify daily/weekly toggles and bounded windows produce deterministic view outputs for fixed API fixtures.
- Verify empty-history state communicates actionable next steps and does not appear as a hard error.
- Verify error state offers retry behavior and preserves dashboard stability.
- Verify trend meaning is understandable without color only (text/icon pairing, assistive labels, keyboard support).
- Verify responsive rendering quality on mobile and desktop layouts.

### Previous Story Intelligence

- Story 3.4 already established dashboard progress cards, loading/error handling, and assistive announcements; extend those patterns rather than replacing them.
- Story 3.3 already exposes daily/weekly trend summaries with typed frontend contracts; consume those contracts directly.

### Git Intelligence Summary

- Recent commits concentrated on deterministic task/progress behavior and end-to-end Epic 1/2 completion, reinforcing a pattern of server-authoritative state and strong test coverage.
- Story 3.5 should continue this pattern by adding trend visualization and momentum context without introducing divergent business logic in the UI.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 3, Story 3.5]
- Epic progression and momentum requirements context (`FR15`, `FR16`, `FR17`, `FR18`, `FR19`, `FR20`, `FR42`, `NFR4`, `NFR7`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md]
- Product motivation and progress visibility goals: [Source: _bmad-output/planning-artifacts/prd.md, Executive Summary; Success Criteria]
- Architecture constraints for deterministic progression, ownership boundaries, API patterns, and accessibility baseline: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; Frontend Architecture]
- UX guidance for momentum visibility, progress trust, responsive behavior, and non-color-only communication: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Core User Experience; Accessibility Considerations]
- Prior implementation baselines: [Source: _bmad-output/implementation-artifacts/3-3-expose-progress-apis-for-xp-streak-and-trend-snapshots.md, _bmad-output/implementation-artifacts/3-4-build-dashboard-progress-components-and-feedback-ui.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed manually (BMAD CLI unavailable in current shell)

### Completion Notes List

- Implemented momentum summary and historical progress table directly in the dashboard using existing `ProgressService` contracts.
- Added bounded daily/weekly controls (30-day default, 12-week default) and request-version deconfliction for deterministic concurrent updates.
- Added explicit loading, empty, and error states for momentum view with retry actions and assistive labels.
- Added non-color-only trend indicators using text plus icon cues and reduced-motion-safe transitions.
- Added dashboard component tests for daily/weekly requests, empty/error states, and trend direction rendering.
- Sprint status updated to set Story 3.5 to `done`.

### File List

- _bmad-output/implementation-artifacts/3-5-implement-momentum-summary-and-historical-progress-view.md
- task-tracker-web/src/app/features/dashboard/dashboard.component.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.spec.ts
- _bmad-output/implementation-artifacts/sprint-status.yaml