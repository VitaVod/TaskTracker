# Story 8.3: Build Level Thresholds and Multi-Band XP Progress Bar

Status: done

## Story

As a user,
I want to see levels and a color-banded XP bar,
so that long-term progression is clear and motivating.

## Acceptance Criteria

1. Given current XP and configured thresholds, when dashboard progress renders, then current level, next threshold, and percent-to-next-level are shown.
2. Given level transitions, when user reaches levels 3, 5, 10, 20, 30, and 50, then configured color bands are applied.
3. Given accessibility requirements, when indicators render, then status meaning is not conveyed by color alone.
4. Given XP changes from completion or reopen events, when updates arrive, then bar and level state refresh without stale intermediate state.

## Tasks / Subtasks

- [x] Add level threshold configuration storage and retrieval (AC: 1, 2)
- [x] Compute level/next-threshold/percentage server-side for consistent UI use (AC: 1)
- [x] Implement dashboard XP bar with threshold color bands (AC: 2)
- [x] Add text/icon states for accessibility non-color cues (AC: 3)
- [x] Add tests for edge thresholds and live update behavior (AC: 4)

## Dev Notes

- Keep level calculation deterministic and cache-safe.
- Avoid duplicate threshold logic between backend and frontend.

### Project Structure Notes

- Backend progress endpoints: task-tracker-api/TaskTracker.Api
- Dashboard UI: task-tracker-web/src/app/features/dashboard

### Testing Requirements

- Threshold boundary tests at each configured milestone.
- Accessibility tests for non-color-only meaning.

### References

- Source briefing: _bmad-output/planning-artifacts/bmad-briefing-2026-05-03.md
- Story inventory: _bmad-output/planning-artifacts/epics.md
