# Story 8.6: Improve Task and Dashboard UX Navigation and Empty States

Status: done

## Story

As a user,
I want clearer routing and empty-state guidance,
so that key actions are obvious across dashboard and task views.

## Acceptance Criteria

1. Given All Tasks filter is selected and no active tasks exist, when list renders, then create-task empty state appears using Active Tasks pattern.
2. Given primary app surfaces, when navigation renders, then header tabs expose dashboard/tasks/momentum/leaderboard/profile routes with active highlighting.
3. Given route deep links and browser history, when users navigate via tabs/back/forward, then route state remains correct.
4. Given task description input on create/edit forms, when resize occurs, then textarea resizes vertically only and layout remains stable.

## Tasks / Subtasks

- [x] Reuse Active Tasks empty-state component in All Tasks zero-active scenario (AC: 1)
- [x] Add header tab navigation for major routes with active state styling (AC: 2, 3)
- [x] Verify route and history behavior for tab-driven navigation (AC: 3)
- [x] Apply vertical-only resize behavior to task description field (AC: 4)
- [x] Add UI tests for empty state, tabs, and textarea behavior (AC: 1, 2, 4)

## Dev Notes

- Preserve existing route paths to avoid breaking bookmarks.
- Keep mobile tab behavior horizontally scrollable if needed.

### Project Structure Notes

- Task list/form UI: task-tracker-web/src/app/features/tasks
- Dashboard/app shell navigation: task-tracker-web/src/app/features/dashboard and shared layout

### Testing Requirements

- Verify All Tasks empty state behavior.
- Verify textarea does not resize horizontally.

### References

- Source briefing: _bmad-output/planning-artifacts/bmad-briefing-2026-05-03.md
- Story inventory: _bmad-output/planning-artifacts/epics.md
