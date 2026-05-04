# Story 8.1: Enforce Progression Integrity for Task State and Deletion Rules

Status: done

## Story

As a user,
I want XP and completed counters to remain fair and deterministic,
so that my progress cannot be lost by destructive or inconsistent task transitions.

## Acceptance Criteria

1. Given a completed task, when delete is requested, then deletion is rejected by business rules and user guidance suggests archive/hide alternatives.
2. Given a task transitions from completed to active, when transition succeeds, then awarded XP is compensated and completed-task counter is decremented exactly once.
3. Given duplicate or retried completion/reopen requests, when processing occurs, then XP and counters remain idempotent.
4. Given state transitions affect streak snapshots, when recalculation executes, then affected day snapshots remain consistent with final task state.

## Tasks / Subtasks

- [x] Enforce completed-task deletion guard in task command path (AC: 1)
  - [x] Return a stable Problem Details app code for blocked completed-task deletion.
  - [x] Ensure UI handles this response and shows actionable guidance.
- [x] Implement deterministic compensation on completed -> active transition (AC: 2, 3)
  - [x] Write compensating XP ledger entry exactly once.
  - [x] Decrement completed-task count exactly once.
- [x] Reconcile daily progression snapshots when transitions are reverted (AC: 4)
  - [x] Recompute or adjust affected local-date snapshot record.
- [x] Add tests for deletion blocking and idempotent transition behavior (AC: 1, 2, 3, 4)

## Dev Notes

- Keep progression logic server-authoritative.
- Preserve SQL Server transaction boundaries so counter and ledger updates commit atomically.

### Project Structure Notes

- API: task-tracker-api/TaskTracker.Api
- Tests: task-tracker-api/tests/TaskTracker.Api.Tests

### Testing Requirements

- Integration tests for completed delete rejection.
- Integration tests for double-submit and retry safety.
- Regression tests for streak and daily snapshot consistency.

### References

- Source briefing: _bmad-output/planning-artifacts/bmad-briefing-2026-05-03.md
- Story inventory: _bmad-output/planning-artifacts/epics.md
