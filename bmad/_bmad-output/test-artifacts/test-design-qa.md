---
workflowStatus: 'completed'
workflowType: 'testarch-test-design'
mode: 'system-level'
author: 'Vitalii'
date: '2026-05-04'
project: 'TaskTracker'
relatedArchitectureDoc: '_bmad-output/test-artifacts/test-design-architecture.md'
---

# Test Design for QA: TaskTracker Core Product Surface

**Purpose:** Test execution recipe for QA. Defines what to test, how to test it, and what QA needs from other teams.

**Date:** 2026-05-04
**Author:** Vitalii
**Status:** Draft
**Project:** bmad

**Related:** See architecture strategy in `_bmad-output/test-artifacts/test-design-architecture.md` for architectural blockers and mitigation ownership.

---

## Executive Summary

**Scope:** End-to-end QA strategy for authentication, task lifecycle, XP/streak consistency, leaderboards, moderation/support paths, and integration parity.

**Risk Summary:**

- Total risks: 8 (5 high-priority score >=6, 3 medium/low)
- Critical categories: DATA, SEC, PERF, TECH

**Coverage Summary:**

- P0 tests: ~14
- P1 tests: ~22
- P2 tests: ~24
- P3 tests: ~8
- Total: ~68 tests (~2.5-4.5 weeks with one QA engineer)

---

## Not in Scope

| Item | Reasoning | Mitigation |
| --- | --- | --- |
| Third-party email provider internals | Provider reliability internals are external to product code ownership. | Validate integration contracts and monitor delivery outcomes. |
| Cloud infrastructure chaos drills | Cross-region fault injection is platform-owned and expensive. | Weekly platform resilience runbooks and drill evidence. |
| Legacy non-MVP modules | Not part of current product scope. | Isolate via routing and monitor for regression. |

---

## Dependencies and Test Blockers

### Backend/Architecture Dependencies (Pre-Implementation)

1. **Deterministic clock and timezone policy** - Backend - pre-implementation
   - Needed for reliable streak/day-boundary assertions.
   - Blocking because DST and cutoff edge cases are otherwise nondeterministic.

2. **Seed/reset contract for account-task-progress states** - Backend - pre-implementation
   - Needed for fast parallel test setup/teardown.
   - Blocking because current setup cost is too high for stable CI.

3. **Cache freshness metadata for shared views** - Backend/Platform - sprint 1
   - Needed to assert acceptable eventual consistency windows.
   - Blocking for reliable leaderboard/global stats assertions.

4. **Correlation ID propagation into audit and support timeline** - Backend/Platform - sprint 1
   - Needed for dispute-trace and forensic assertions.
   - Blocking for support and moderation reliability checks.

### QA Infrastructure Setup (Pre-Implementation)

1. Test factories for users, tasks, completion events, and leaderboard snapshots.
2. Parallel-safe cleanup fixture with deterministic data ownership per worker.
3. Tagged test conventions (`@P0`, `@P1`, `@SEC`, `@DATA`, `@PERF`).

```typescript
import { test } from '@seontechnologies/playwright-utils/api-request/fixtures';
import { expect } from '@playwright/test';

test('@P0 @DATA completion idempotency prevents duplicate XP', async ({ apiRequest }) => {
  const payload = { taskId: 'task-1', idempotencyKey: 'k-123' };

  const first = await apiRequest({ method: 'POST', path: '/api/v1/tasks/complete', body: payload });
  const second = await apiRequest({ method: 'POST', path: '/api/v1/tasks/complete', body: payload });

  expect(first.status).toBe(200);
  expect(second.status).toBe(200);

  const progress = await apiRequest({ method: 'GET', path: '/api/v1/progress/summary' });
  expect(progress.body.xpDeltaForLastCompletion).toBe(1);
});
```

---

## Risk Assessment

This section summarizes QA coverage for risks defined in the architecture document.

### High-Priority Risks (Score >=6)

| Risk ID | Category | Description | Score | QA Test Coverage |
| --- | --- | --- | --- | --- |
| R-001 | DATA | Duplicate completion event processing | 9 | API idempotency replay tests + E2E post-condition checks |
| R-002 | SEC | Cross-user data exposure through ownership-policy gaps | 9 | Negative API policy matrix + E2E role-routing checks |
| R-003 | PERF | Shared reads degrade under spikes | 6 | API latency assertions + nightly load baseline |
| R-004 | OPS | Cache invalidation lag causes stale shared views | 6 | Read-after-write tolerance tests with staleness metadata |
| R-005 | TECH | Timezone policy mismatch for streak outcomes | 6 | Clock-controlled API tests + boundary E2E scenarios |

### Medium/Low-Priority Risks

| Risk ID | Category | Description | Score | QA Test Coverage |
| --- | --- | --- | --- | --- |
| R-006 | OPS | Transactional email retry reliability | 4 | Retry-path API tests + delivery event verification |
| R-007 | DATA | Integration retry duplicate task creation | 4 | Integration idempotency API tests |
| R-008 | SEC | Audit records missing required reason/correlation fields | 3 | Admin/support action audit-contract tests |

---

## Entry Criteria

- [ ] Requirements and assumptions aligned across QA, Dev, PM.
- [ ] Test environments and secrets provisioned.
- [ ] Seed/reset capability available.
- [ ] Correlation and audit schema available for privileged paths.
- [ ] Feature build deployed to test environment.

## Exit Criteria

- [ ] P0 tests pass at 100%.
- [ ] P1 tests pass at >=95% (remaining failures triaged and accepted).
- [ ] No open high-severity defects tied to R-001..R-005.
- [ ] Coverage judged sufficient by QA and engineering leads.

---

## Test Coverage Plan

**Note:** P0/P1/P2/P3 represent risk priority and business criticality, not execution timing.

### P0 (Critical)

**Criteria:** Blocks core functionality + high risk (>=6) + no workaround

| Test ID | Requirement | Test Level | Risk Link | Notes |
| --- | --- | --- | --- | --- |
| P0-001 | Task completion awards XP exactly once for replayed completion request | API | R-001 | Idempotency key replay |
| P0-002 | Cross-user task read/write is denied for normal user role | API | R-002 | BOLA protection |
| P0-003 | Token/session expiration and revocation enforced | API | R-002 | Auth lifecycle |
| P0-004 | Streak continuation/reset at day boundary is deterministic | API | R-005 | Clock-controlled |
| P0-005 | User completes task and sees consistent task/XP/streak state | E2E | R-001,R-005 | Core value loop |
| P0-006 | Leaderboard exposes only approved public identity fields | API | R-002 | Privacy guardrail |

### P1 (High)

**Criteria:** Important feature paths + medium/high risk + common workflows

| Test ID | Requirement | Test Level | Risk Link | Notes |
| --- | --- | --- | --- | --- |
| P1-001 | Global statistics update within accepted staleness window after completion | API | R-004 | Cache visibility |
| P1-002 | Leaderboard tie-break ordering is deterministic | API | R-003 | Ranking consistency |
| P1-003 | Password recovery token is single-use and time-limited | API | R-002,R-006 | Security + reliability |
| P1-004 | Support timeline explains XP/streak dispute with correlated events | API | R-008 | Explainability |
| P1-005 | Integration task-create retry remains idempotent | API | R-007 | External parity |
| P1-006 | Dashboard surfaces progress feedback accessibly after completion | Component | R-005 | WCAG signal |

### P2 (Medium)

**Criteria:** Secondary flows + low/medium risk + edge-case and regression coverage

| Test ID | Requirement | Test Level | Risk Link | Notes |
| --- | --- | --- | --- | --- |
| P2-001 | Notification preference updates govern reminder send behavior | API | R-006 | Preference enforcement |
| P2-002 | Admin moderation action writes immutable audit record with reason | API | R-008 | Audit integrity |
| P2-003 | Empty/loading/error UI states remain accessible and informative | Component | R-005 | UX resilience |
| P2-004 | Public profile participation controls affect leaderboard visibility | E2E | R-002 | Privacy behavior |
| P2-005 | Global stats panel remains stable when partial backend failures occur | E2E | R-003 | Degradation path |

### P3 (Low)

**Criteria:** Nice-to-have + exploratory + benchmark/soak checks

| Test ID | Requirement | Test Level | Risk Link | Notes |
| --- | --- | --- | --- | --- |
| P3-001 | Long-run weekly trend/momentum rendering sanity checks | Component | R-003 | Visual consistency |
| P3-002 | Exploratory keyboard-only interaction sweep | E2E | R-005 | Accessibility confidence |
| P3-003 | Non-critical copy and guidance message regression checks | Component | R-009 | Engagement polish |

---

## Execution Strategy

**Philosophy:** Run all functional suites in PR pipelines unless tests are expensive or long-running.

### Every PR: Playwright Functional Suites (~10-15 min)

- Run API, E2E, and component-level functional tests with parallelization.
- Includes P0/P1 plus selected fast P2 cases.

### Nightly: Performance and Reliability Suites (~30-60 min)

- Run load and staleness-window checks for leaderboard/global statistics.
- Run heavier replay and retry stress combinations.

### Weekly: Long-Running and Chaos Suites (hours)

- Run extended endurance and platform-failover validations.
- Include manual cross-team verification where automation is insufficient.

---

## QA Effort Estimate

| Priority | Count | Effort Range | Notes |
| --- | --- | --- | --- |
| P0 | ~14 | ~0.8-1.4 weeks | Security/idempotency/day-boundary complexity |
| P1 | ~22 | ~0.9-1.6 weeks | Integration and determinism coverage |
| P2 | ~24 | ~0.5-1.0 weeks | Regression and edge handling |
| P3 | ~8 | ~0.3-0.5 weeks | Exploratory and benchmark checks |
| Total | ~68 | ~2.5-4.5 weeks | One QA engineer, includes CI stabilization |

Assumptions:

- Seed/reset tooling and deterministic clock contract are delivered.
- Test data factories are shared across API and E2E.
- Ongoing maintenance is outside this estimate.

---

## Implementation Planning Handoff

| Work Item | Owner | Target Milestone | Dependencies and Notes |
| --- | --- | --- | --- |
| Deliver seed/reset interfaces for non-prod | Backend | Sprint 1 | Required for stable parallel tests |
| Introduce deterministic clock abstraction | Backend | Sprint 1 | Required for streak boundary tests |
| Add staleness metadata to shared read endpoints | Backend | Sprint 1 | Required for cache-consistency assertions |
| Add correlation propagation and audit schema constraints | Backend/Platform | Sprint 1 | Required for support traceability tests |
| Implement P0/P1 tagged Playwright suites in CI | QA | Sprint 1-2 | Depends on blocker completion |

---

## Interworking and Regression

| Service/Component | Impact | Regression Scope | Validation Steps |
| --- | --- | --- | --- |
| Identity/Auth | Session and role policy correctness | Auth API policy suite | Verify 401/403 matrix and revocation behavior |
| Task Domain | Deterministic state transitions | Task CRUD and completion API suite | Validate ownership and state integrity |
| Progress Engine | XP/streak correctness | Completion + streak API suite | Validate replay and boundary scenarios |
| Shared Read Models | Leaderboard/global stats consistency | Read-model API + dashboard E2E | Validate staleness budget and rank correctness |
| Notifications | Recovery and reminders reliability | Email pipeline tests | Verify retries and preference gating |

---

## Appendix A: Code Examples and Tagging

```typescript
import { test } from '@seontechnologies/playwright-utils/api-request/fixtures';
import { expect } from '@playwright/test';

test('@P1 @SEC user cannot access another user task', async ({ apiRequest }) => {
  const response = await apiRequest({
    method: 'GET',
    path: '/api/v1/tasks/task-owned-by-other-user',
  });

  expect([401, 403]).toContain(response.status);
});
```

```bash
npx playwright test --grep @P0
npx playwright test --grep "@P0|@P1"
npx playwright test
```

---

## Appendix B: Knowledge Base References

- `.github/skills/bmad-testarch-test-design/resources/knowledge/risk-governance.md`
- `.github/skills/bmad-testarch-test-design/resources/knowledge/test-levels-framework.md`
- `.github/skills/bmad-testarch-test-design/resources/knowledge/test-quality.md`
- `.github/skills/bmad-testarch-test-design/resources/knowledge/adr-quality-readiness-checklist.md`
- `.github/skills/bmad-testarch-test-design/resources/knowledge/overview.md`
- `.github/skills/bmad-testarch-test-design/resources/knowledge/api-request.md`
