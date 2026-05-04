# Story 8.2: Extend Task Model with Difficulty and Planning Metadata

Status: done

## Story

As a user,
I want to classify tasks by difficulty, energy, and context,
so that planning is more realistic and rewards match effort.

## Acceptance Criteria

1. Given task create or update, when metadata is submitted, then difficulty, energy level, context tag, effort points, and predicted duration are validated and stored.
2. Given task completion, when XP is awarded, then difficulty mapping applies deterministically: easy 10, medium 20, hard 30.
3. Given historical completions, when difficulty mapping is introduced, then historical awarded XP remains unchanged unless task is explicitly reopened and recomputed by rule.
4. Given task lists, when filters are applied, then users can filter by context and energy.

## Tasks / Subtasks

- [x] Add domain and persistence fields for planning metadata (AC: 1)
- [x] Extend API contracts and validators for new fields (AC: 1)
- [x] Apply difficulty-to-XP mapping in completion flow (AC: 2)
- [x] Implement filter query support for context and energy (AC: 4)
- [x] Add migrations and backfill defaults for existing records (AC: 1, 3)
- [x] Add unit/integration tests for award mapping and metadata filtering (AC: 2, 4)

## Dev Notes

- Use enum-backed value constraints with clear API serialization rules.
- Keep completion award idempotency tied to ledger correlation identifiers.

### Project Structure Notes

- API models/contracts: task-tracker-api/TaskTracker.Api
- Web task forms/filters: task-tracker-web/src/app/features/tasks

### Testing Requirements

- Validate mapping easy/medium/hard => 10/20/30.
- Validate metadata defaults for legacy tasks.
- Validate filters at API and UI integration levels.

### References

- Source briefing: _bmad-output/planning-artifacts/bmad-briefing-2026-05-03.md
- Story inventory: _bmad-output/planning-artifacts/epics.md
