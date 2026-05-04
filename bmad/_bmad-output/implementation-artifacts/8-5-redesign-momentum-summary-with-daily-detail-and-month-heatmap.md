# Story 8.5: Redesign Momentum Summary with Daily Detail and Month Heatmap

Status: done

## Story

As a user,
I want a readable momentum overview with drill-down,
so that I can understand daily progress trends and act on them.

## Acceptance Criteria

1. Given momentum summary loads, when historical data exists, then responsive card/list layout replaces unwrapped table behavior.
2. Given summary items are rendered, when user selects a day, then the app routes to day-detail statistics page.
3. Given month heatmap renders, when activity exists, then cell color intensity reflects daily activity level.
4. Given accessibility requirements, when heatmap and trend cards are used, then keyboard navigation and assistive labels are present.

## Tasks / Subtasks

- [x] Refactor momentum summary layout into responsive card/list structure (AC: 1)
- [x] Add click-through routing to day-detail page (AC: 2)
- [x] Build month heatmap component with deterministic intensity scale (AC: 3)
- [x] Add day-detail view with completed tasks, XP, streak impact, and momentum score (AC: 2)
- [x] Add accessibility and keyboard support tests (AC: 4)

## Dev Notes

- Reuse existing trend endpoints and avoid duplicating server business logic in UI.
- Keep default heatmap window aligned to last month.

### Project Structure Notes

- Dashboard and momentum UI: task-tracker-web/src/app/features/dashboard
- Progress API contracts: task-tracker-web/src/app/shared/services and models

### Testing Requirements

- Component tests for heatmap intensity mapping.
- Routing tests for day-detail navigation.

### References

- Source briefing: _bmad-output/planning-artifacts/bmad-briefing-2026-05-03.md
- Story inventory: _bmad-output/planning-artifacts/epics.md

## Dev Agent Record

### Completion Notes

- Replaced the momentum table with a responsive trend card list that preserves historical data visibility on mobile and desktop.
- Added day drill-down routing from momentum cards and keyboard activation support for Enter/Space.
- Built a dedicated monthly heatmap component with deterministic intensity levels based on per-day completion counts.
- Added assistive labels and arrow-key navigation behavior for heatmap cells and trend cards.
- Added day detail page for selected date showing completed tasks, XP granted, streak bonus impact, and computed momentum score.

### Test Evidence

- `npx ng test --watch=false --browsers=ChromeHeadless --no-progress` (pass)

## File List

- task-tracker-web/src/app/features/dashboard/dashboard.component.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.spec.ts
- task-tracker-web/src/app/features/dashboard/momentum-heatmap.component.ts
- task-tracker-web/src/app/features/dashboard/momentum-heatmap.component.spec.ts
- task-tracker-web/src/app/features/dashboard/day-detail.component.ts
- task-tracker-web/src/app/features/dashboard/day-detail.component.spec.ts
- task-tracker-web/src/app/app.routes.ts
- _bmad-output/implementation-artifacts/8-5-redesign-momentum-summary-with-daily-detail-and-month-heatmap.md
- _bmad-output/implementation-artifacts/sprint-status.yaml

## Change Log

- 2026-05-03: Implemented responsive momentum card/list layout, month heatmap component, day-detail route/page, and accessibility-focused keyboard/aria tests. Story marked done.
