# Story 3.1: Build XP Ledger and Idempotent Completion Processing

Status: ready-for-dev

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want XP granted exactly once for an eligible completion,
so that progress feels fair and trustworthy.

## Acceptance Criteria

1. Given a task completion event with idempotency key, when progression command runs, then XP ledger entry is created at most once per eligible event.
2. Retries for the same eligible completion return a consistent result and do not produce duplicate XP grants.

## Tasks / Subtasks

- [ ] Define progression command contract for XP grant from completion event (AC: 1, 2)
  - [ ] Add request/response contract for processing a completion event by idempotency key and actor context.
  - [ ] Define deterministic response payload fields that can be reused by follow-up streak/progress stories.
  - [ ] Ensure API/app contract remains aligned with existing `/api/v1` + Problem Details conventions.

- [ ] Implement idempotent XP ledger write path in backend (AC: 1, 2)
  - [ ] Add or extend persistence entities for XP ledger records keyed by completion event identity.
  - [ ] Enforce database uniqueness constraints/indexes to prevent duplicate XP ledger writes during retries.
  - [ ] Persist only server-authoritative values (no frontend-computed XP outcomes).

- [ ] Wire progression processing to completion event source of truth (AC: 1)
  - [ ] Reuse `TaskCompletionEvent` data established in Story 2.4 as the trigger input for XP processing.
  - [ ] Validate ownership and eligibility before writing XP records.
  - [ ] Keep operation transactional so completion-to-ledger state cannot partially commit.

- [ ] Return deterministic replay result for duplicate submissions (AC: 2)
  - [ ] If event was already processed, return original outcome snapshot rather than creating new rows.
  - [ ] Ensure repeated calls are stable across transient retries/reconnects.
  - [ ] Preserve traceability fields (event id, correlation/trace id) for support diagnostics.

- [ ] Add regression-safe API and persistence tests for idempotency behavior (AC: 1, 2)
  - [ ] Verify single eligible completion produces exactly one XP ledger entry.
  - [ ] Verify repeated processing of same idempotency key returns consistent result and no duplicate grants.
  - [ ] Verify ownership/auth failures and invalid inputs map to RFC 7807 Problem Details with stable `code` and `traceId`.

- [ ] Add frontend/service integration hooks needed for immediate momentum feedback (AC: 2)
  - [ ] Expose deterministic completion outcome fields required by dashboard feedback components.
  - [ ] Keep server state authoritative for XP/streak-related UI updates.
  - [ ] Avoid introducing optimistic progression state that can diverge from backend truth.

## Dev Notes

- Story 2.4 already introduced completion idempotency and a persisted `TaskCompletionEvent`; Story 3.1 must build directly on that event path rather than creating a second completion source.
- SQL Server remains mandatory for persistence and uniqueness guarantees in this story, using EF Core SQL Server provider and migration workflow already established.
- Progression behavior must remain deterministic and replay-safe; retries should never create additional XP grants for the same eligible event.
- Preserve existing API standards: versioned routes under `/api/v1`, RFC 7807 Problem Details, and stable `code` + `traceId` fields.
- Keep ownership and authorization enforcement server-side; user-scoped progress data cannot be exposed cross-account.

### Project Structure Notes

- Backend expected touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/`
  - `task-tracker-api/TaskTracker.Api/Features/Tasks/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`

- Frontend expected touch points:
  - `task-tracker-web/src/app/features/dashboard/`
  - `task-tracker-web/src/app/features/tasks/`
  - `task-tracker-web/src/app/shared/models/`
  - `task-tracker-web/src/app/shared/services/`

- Architecture planning docs define future layered projects (`TaskTracker.Application`, `TaskTracker.Domain`, `TaskTracker.Infrastructure`), but current implementation is feature-first within `TaskTracker.Api`; follow the implemented structure for this story.

### Testing Requirements

- Verify first-time eligible completion processing writes one and only one XP ledger entry.
- Verify duplicate submissions/retries for the same idempotency key replay the same result without additional XP rows.
- Verify transaction boundaries prevent partial commit states between completion-event consumption and XP ledger write.
- Verify unauthorized/forbidden ownership cases cannot mutate XP ledger state.
- Verify invalid request/idempotency inputs return RFC 7807 payload with stable `code` and `traceId`.
- Verify API response latency and deterministic behavior support UX requirement for near-immediate completion feedback.

### Git Intelligence Summary

- Recent commits show deterministic completion/idempotency patterns are already established in task flows and should be reused rather than replaced:
  - `feat(tasks): implement deterministic completion toggle with idempotency hardening`
  - `feat(tasks): implement story 2.3 task update and organizational attributes`
- Reuse existing repository and test conventions from those task stories to avoid introducing parallel progression handling styles.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 3, Story 3.1]
- Epic requirement coverage and non-functional constraints (`FR15`, `FR16`, `FR17`, `FR18`, `FR19`, `FR20`, `FR42`, `NFR3`, `NFR4`, `NFR6`, `NFR7`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory and Epic 3]
- Product-level idempotency, deterministic progression, and trust goals: [Source: _bmad-output/planning-artifacts/prd.md, MVP Success Criteria; Progress, XP, and Streaks; Non-Functional Requirements]
- Architecture constraints for SQL Server, idempotency, timezone policy, event payload shape, and consistency rules: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; Data Architecture; Communication Patterns; Enforcement Guidelines]
- UX expectations for immediate feedback, trust, and accessible status communication: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Core User Experience; Experience Principles; Accessibility]
- Previous implementation baseline: [Source: _bmad-output/implementation-artifacts/2-4-implement-task-completion-toggle-with-deterministic-state.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Story 3.1 drafted with implementation tasks, architecture guardrails, and test requirements for XP ledger idempotency.
- Previous-story context (Story 2.4 completion event/idempotency baseline) captured to prevent parallel or conflicting implementations.
- Sprint status updated to move Epic 3 into active execution and mark Story 3.1 as `ready-for-dev`.

### File List

- _bmad-output/implementation-artifacts/3-1-build-xp-ledger-and-idempotent-completion-processing.md
- _bmad-output/implementation-artifacts/sprint-status.yaml
