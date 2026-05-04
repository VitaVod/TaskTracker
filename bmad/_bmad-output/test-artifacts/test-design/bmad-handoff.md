---
title: 'TEA Test Design -> BMAD Handoff Document'
version: '1.0'
workflowType: 'testarch-test-design-handoff'
sourceWorkflow: 'testarch-test-design'
generatedBy: 'TEA Master Test Architect'
generatedAt: '2026-05-04'
projectName: 'bmad'
---

# TEA -> BMAD Integration Handoff

## Purpose

This document bridges TEA test design outputs with BMAD epic and story decomposition so quality requirements and risk controls are carried into implementation planning.

## TEA Artifacts Inventory

| Artifact | Path | BMAD Integration Point |
| --- | --- | --- |
| Test Design Architecture | `_bmad-output/test-artifacts/test-design-architecture.md` | Epic quality constraints, pre-implementation blockers |
| Test Design QA | `_bmad-output/test-artifacts/test-design-qa.md` | Story acceptance and test automation requirements |
| Risk Register | Embedded in both docs | Story risk priority and release gating |

## Epic-Level Integration Guidance

### Risk References

Promote the following to epic-level quality gates:

- R-001 DATA: completion idempotency and duplicate-event handling
- R-002 SEC: ownership and role authorization boundaries
- R-003 PERF: leaderboard/global stats performance under spikes
- R-004 OPS: cache invalidation and freshness observability
- R-005 TECH: timezone and day-boundary deterministic behavior

### Quality Gates

- No story is marked done until linked P0 scenarios have passing evidence.
- No release candidate passes gate with unresolved R-001 or R-002 risks.
- P0 must remain 100% passing; P1 must remain >=95% passing.
- Blockers B-001..B-004 must be complete before broader QA automation scale-up.

## Story-Level Integration Guidance

### P0/P1 Test Scenarios -> Story Acceptance Criteria

Embed these in story acceptance criteria where relevant:

- Completion replay does not duplicate XP/streak updates.
- Cross-user task/progress access is denied with correct status codes.
- Day-boundary streak behavior is deterministic under timezone edges.
- Shared read models expose acceptable staleness and deterministic ordering.
- Privileged operations emit auditable reason and correlation fields.

### Data-TestId Requirements

For UI stories touching key flows, include stable selectors:

- Authentication: login-submit, logout-action, password-recovery-submit
- Task flow: task-create, task-complete-toggle, task-delete-confirm
- Progress feedback: xp-total, streak-status, progress-summary
- Leaderboard/stats: leaderboard-row, leaderboard-rank, global-stats-panel

## Risk-to-Story Mapping

| Risk ID | Category | P x I | Recommended Story and Epic | Test Level |
| --- | --- | --- | --- | --- |
| R-001 | DATA | 3 x 3 | Task completion state transition stories (Epic 2/8) | API + E2E |
| R-002 | SEC | 3 x 3 | Auth and authorization baseline stories (Epic 1/4/6) | API + E2E |
| R-003 | PERF | 2 x 3 | Leaderboard and global stats read-model stories (Epic 4) | API + Perf |
| R-004 | OPS | 2 x 3 | Cache and invalidation strategy stories (Epic 4) | API |
| R-005 | TECH | 2 x 3 | Streak rule and timezone policy stories (Epic 3/8) | API + Unit |
| R-006 | OPS | 2 x 2 | Notification and recovery email stories (Epic 5) | API |
| R-007 | DATA | 2 x 2 | Integration idempotency and retry handling stories (Epic 7) | API |
| R-008 | SEC | 1 x 3 | Audit logging for privileged actions stories (Epic 6) | API |

## Recommended BMAD -> TEA Workflow Sequence

1. TEA test design (completed)
2. BMAD create epics and stories using this handoff
3. TEA ATDD for selected P0 stories
4. BMAD implementation with test-first guidance
5. TEA automate to expand coverage
6. TEA trace to verify requirement-to-test completeness

## Phase Transition Quality Gates

| From Phase | To Phase | Gate Criteria |
| --- | --- | --- |
| Test Design | Epic and Story Creation | P0 risks have mitigation and owner |
| Epic and Story Creation | ATDD | Stories include test-ready acceptance criteria |
| ATDD | Implementation | Failing acceptance tests exist for target stories |
| Implementation | Test Automation | P0 scenarios pass in CI |
| Test Automation | Release | Coverage and risk gates meet policy |
