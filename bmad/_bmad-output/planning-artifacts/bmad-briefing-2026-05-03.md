---
title: "BMAD Briefing: Progression, Momentum, Profiles, and UX Reliability"
status: "draft"
created: "2026-05-03"
updated: "2026-05-03"
project: "Task Tracker"
stack:
  backend: "ASP.NET"
  frontend: "Angular"
  database: "SQL Server"
---

# BMAD Briefing: Progression, Momentum, Profiles, and UX Reliability

## Objective
Ship a user-centered progression and engagement upgrade while fixing progression integrity and UX regressions.

## Why Now
Current functionality works but has trust and usability gaps:
- XP can be rolled back by deleting completed tasks, which breaks progression trust.
- Momentum summary is hard to read and does not support drill-down.
- Routing and profile preferences are less intuitive than expected.
- Email recovery/notification reliability is inconsistent.

## Outcomes
- Progression feels fair, visible, and motivating.
- Streak behavior is deterministic and understandable.
- Momentum insights are readable and actionable.
- Task creation and navigation paths are clear in all major screens.
- Public profile behavior aligns with privacy settings.

## Requested Scope

### Progression and Streak
- Add XP levels with color bands at levels: 3, 5, 10, 20, 30, 50.
- Add a visual XP progress bar tied to current and next level.
- Discuss and formalize streak logic with timezone policy.
- Add one streak recovery token per week.
- Add near-miss nudge when user is one task away from preserving streak tier.

### Momentum Experience
- Replace unwrapped momentum table with user-friendly layout.
- Make momentum summary items clickable.
- Add a day details page for selected date.
- Add a GitHub-like month contribution heatmap with activity intensity colors.

### Task List and Task Model
- In "All tasks", when there are no active tasks, show the same create-task empty state used by "Active tasks".
- Add planning metadata:
  - Energy level: low, medium, high.
  - Context tag: work, home, phone, offline.
  - Effort points.
  - Predicted duration.
- Add difficulty-driven XP:
  - Easy: 10 XP
  - Medium: 20 XP
  - Hard: 30 XP

### Dashboard and Navigation
- Make dashboard routing more user-friendly.
- Add header tabs for primary areas (dashboard/tasks/momentum/leaderboard/profile).

### Profile and Preferences
- Redesign leaderboard participation dropdown for clearer UX.
- Add secure email change flow with current password confirmation.

### Public Profiles
- Add user profile pages with core statistics.
- If user is not public, show: "User is Anonymous participant. Statistics of this user will not be displayed."

## Bug Fixes
1. XP and completion integrity
- Prevent removal of completed tasks.
- Keep XP gained from completed tasks unless task is explicitly reopened.
- If task is moved completed -> active:
  - Revert awarded XP
  - Decrease completed task counter

2. Task description resize
- Make description textarea vertically resizable only.

3. Email reliability
- Fix recovery and notification email delivery path and configuration.

## Product Rules (Proposed Defaults)
- A streak day is preserved when at least one task is completed in user local date.
- Streak evaluation uses user timezone saved in profile preferences.
- Recovery token is auto-consumed on first missed day if available.
- Completed task deletion is blocked; archive/hide remains allowed.
- XP award operations are idempotent via ledger correlation identifiers.

## Technical Notes
- Persist progression and streak updates via deterministic server-side events.
- Keep leaderboard/privacy checks enforced at API level, not only UI level.
- Ensure SQL Server schema migration covers:
  - XP ledger idempotency
  - Daily snapshots for momentum/heatmap
  - Recovery token lifecycle
  - Task metadata extensions
  - Email change request verification

## Acceptance Highlights
- XP does not drop when completed task is deleted because deletion is blocked.
- Reopen completed task correctly compensates XP and counters.
- Momentum view is responsive and readable on desktop/mobile.
- Day heatmap opens detailed view for selected date.
- "All tasks" empty state offers create action when active count is zero.
- Email change requires password and verification before commit.
- Anonymous participant profile never exposes statistics.

## Delivery Plan
1. Stabilization and UX fixes
- Bug fixes (XP integrity, textarea resize, empty states, nav tabs, preference control updates).

2. Progression and streak upgrade
- Levels, XP bar, streak logic formalization, weekly recovery token, near-miss nudge.

3. Momentum and profile expansion
- Heatmap + daily detail, public profile behavior, profile statistics exposure rules.

## Risks
- Streak logic confusion without clear timezone messaging.
- Notification fatigue if near-miss nudges are too frequent.
- Migration drift if existing XP events are not backfilled consistently.

## Open Decisions
- Should recovery token consumption be automatic or user-triggered?
- Should streak tiers be fixed globally or configurable per user segment?
- Should profile URLs use username slug or stable user id?
