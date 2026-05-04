---
workflowStatus: 'completed'
workflowType: 'testarch-test-design'
mode: 'system-level'
author: 'Vitalii'
date: '2026-05-04'
project: 'TaskTracker'
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
---

# Test Design for Architecture: TaskTracker Core Product Surface

**Purpose:** Architectural concerns, testability gaps, and NFR requirements for review by Architecture and Engineering teams. This document is the contract for what must be in place before QA can execute quickly and deterministically.

**Date:** 2026-05-04
**Author:** Vitalii
**Status:** Architecture Review Pending
**Project:** bmad
**PRD Reference:** _bmad-output/planning-artifacts/prd.md
**ADR Reference:** _bmad-output/planning-artifacts/architecture.md

---

## Executive Summary

**Scope:** System-level testability and risk strategy for authentication, task lifecycle, XP/streak processing, leaderboards, admin/support operations, notifications, and external integrations.

**Business Context (from PRD):**

- Problem: Users abandon planning tools due to weak reinforcement loops.
- Outcome dependency: deterministic and trustworthy progress updates (XP/streak/ranking).
- Primary risk: trust erosion if progress, fairness, or privacy guarantees are inconsistent.

**Architecture (from architecture document):**

- Modular monolith with domain boundaries.
- SQL Server + EF Core, cache-backed read models for shared views.
- JWT auth, role policies (user/admin/support), immutable audit trail requirement.
- Idempotent completion processing and timezone-aware streak evaluation.

**Risk Summary:**

- Total risks: 8
- High-priority (score >=6): 5
- Test effort footprint: medium-high due to deterministic state and cross-surface consistency constraints.

---

## Quick Guide

### BLOCKERS - Team Must Decide (Cannot Proceed Without)

1. **B-001 Deterministic Time Provider** - Introduce a single server-side time abstraction for streak boundary logic and support replay.
2. **B-002 Test Data Seeding Interface** - Provide controlled seed/reset mechanism for account, task, completion, and ranking data.
3. **B-003 Cache Freshness Observability** - Expose version/age metadata for leaderboard and global statistics responses.
4. **B-004 Audit Correlation Contract** - Standardize correlation ID propagation across API logs, audit entries, and support timeline reads.

**What we need from team:** Complete these four items before implementation hardens and test automation scales.

### HIGH PRIORITY - Team Should Validate (Recommendation, Team Approves)

1. **R-001 DATA** - Enforce idempotency and conflict-safe completion transaction boundaries.
2. **R-002 SEC** - Harden object ownership and role policy checks at every protected endpoint.
3. **R-003 PERF** - Define response SLO and staleness envelopes for shared reads under burst traffic.
4. **R-005 TECH** - Align timezone/day-boundary policy between API computation and UI rendering.

### INFO ONLY - Solutions Provided

1. Risk-based split across E2E, API, component, and unit tests.
2. PR-first execution for functional suites; expensive suites deferred to nightly/weekly.
3. Quality gates: P0 at 100%, P1 at >=95%, high-risk mitigations mandatory before release.

---

## Risk Assessment

**Total risks identified:** 8 (5 high, 3 medium/low)

### High-Priority Risks (Score >=6)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner | Timeline |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R-001 | DATA | Duplicate completion events can grant duplicate XP and inconsistent streak state. | 3 | 3 | 9 | Idempotency key uniqueness + transactional guard + replay-safe handlers. | Backend | Pre-implementation |
| R-002 | SEC | Broken object-level authorization can expose cross-user task/progress data. | 3 | 3 | 9 | Central ownership policy checks + endpoint-level authorization tests in CI. | Backend/Security | Pre-implementation |
| R-003 | PERF | Leaderboard/global statistics queries can degrade under read spikes. | 2 | 3 | 6 | Read-model caching + hot-key protection + latency SLO alerts. | Backend/Platform | Sprint 1 |
| R-004 | OPS | Cache invalidation lag can show stale rank/progress after writes. | 2 | 3 | 6 | Explicit invalidation events + response staleness metadata + monitoring. | Backend/Platform | Sprint 1 |
| R-005 | TECH | Timezone cutoff mismatch can produce streak disputes and support load. | 2 | 3 | 6 | Shared timezone policy contract + fixed test clock + edge-case fixtures. | Backend/Frontend | Sprint 1 |

### Medium-Priority Risks (Score 3-5)

| Risk ID | Category | Description | Probability | Impact | Score | Mitigation | Owner |
| --- | --- | --- | --- | --- | --- | --- | --- |
| R-006 | OPS | Transactional email retry/monitoring gaps can break recovery flow. | 2 | 2 | 4 | Delivery retries, dead-letter handling, and alerting. | Platform | Sprint 2 |
| R-007 | DATA | Integration retries may create duplicate tasks without strict dedup. | 2 | 2 | 4 | Integration idempotency keys + dedup indexes + retry-safe handlers. | Backend | Sprint 2 |
| R-008 | SEC | Incomplete audit reason/correlation fields reduce forensic quality. | 1 | 3 | 3 | Mandatory reason codes and correlation IDs for privileged actions. | Backend/Security | Sprint 2 |

### Low-Priority Risks (Score 1-2)

| Risk ID | Category | Description | Probability | Impact | Score | Action |
| --- | --- | --- | --- | --- | --- | --- |
| R-009 | BUS | Non-critical motivational UI copy changes may reduce engagement temporarily. | 1 | 1 | 1 | Monitor |

---

## Testability Concerns and Architectural Gaps

### ACTIONABLE CONCERNS

#### 1. Blockers to Fast Feedback

| Concern | Impact | What Architecture Must Provide | Owner | Timeline |
| --- | --- | --- | --- | --- |
| Deterministic time abstraction absent | Streak tests become flaky around day boundaries and DST. | Injectable server clock and explicit timezone policy source. | Backend | Pre-implementation |
| No seed/reset contract for integration states | Setup overhead blocks parallel API/E2E suites. | Seed/reset endpoints or scriptable test harness in non-prod. | Backend | Pre-implementation |
| Cache freshness is opaque | Cannot assert read-after-write consistency windows. | Response metadata for data version and generated-at timestamp. | Backend | Sprint 1 |
| Correlation not guaranteed end-to-end | Support and QA cannot reliably trace disputed events. | Correlation ID standard persisted in logs and audit records. | Backend/Platform | Sprint 1 |

#### 2. Architectural Improvements Needed

1. **Idempotency contract hardening**
   - Current problem: completion and integration retries risk duplicate writes.
   - Required change: unique dedup keys + transactional upsert rules.
   - Impact if not fixed: user trust loss from inconsistent progression.
   - Owner: Backend.
   - Timeline: pre-implementation.

2. **Authorization policy centralization**
   - Current problem: per-endpoint drift risk.
   - Required change: common policy primitives for ownership and role checks.
   - Impact if not fixed: cross-user data exposure and policy bypass.
   - Owner: Backend/Security.
   - Timeline: pre-implementation.

### Testability Assessment Summary

#### What Works Well

- API-first design supports headless automation for most business logic.
- Explicit role model (user/admin/support) enables policy-focused negative testing.
- Architectural intent already includes idempotency and auditability requirements.

#### Accepted Trade-offs (No Action Required)

- Temporary use of synthetic seed scripts is acceptable if seed endpoints are delayed, provided scripts are deterministic and versioned.

---

## Risk Mitigation Plans (High-Priority)

### R-001 Duplicate completion processing (Score 9)

1. Introduce unique idempotency constraint on completion events.
2. Make XP/streak updates part of one transaction boundary.
3. Add replay scenario in CI to prove deterministic post-condition.

Owner: Backend  
Timeline: Pre-implementation  
Status: Planned  
Verification: Replay test suite shows no duplicate XP/streak mutation.

### R-002 Object-level authorization bypass (Score 9)

1. Enforce ownership policy in all task/progress endpoints.
2. Add deny-by-default for unknown roles/scopes.
3. Add negative policy tests for user/admin/support permutations.

Owner: Backend/Security  
Timeline: Pre-implementation  
Status: Planned  
Verification: Security suite confirms 401/403 behavior and no cross-user access.

### R-003 Shared read degradation (Score 6)

1. Define SLO: leaderboard/global stats p95 <= 3s under target load.
2. Add cache and query optimization on ranking read paths.
3. Alert on latency/staleness budget breach.

Owner: Backend/Platform  
Timeline: Sprint 1  
Status: Planned  
Verification: Load tests and telemetry meet SLO in staging.

---

## Assumptions and Dependencies

### Assumptions

1. SQL Server remains the primary data store for transactional and read-model persistence.
2. JWT and role claims are authoritative for API authorization decisions.
3. Non-production environments can support isolated synthetic seed data.

### Dependencies

1. Backend must expose deterministic time provider and policy before QA automation scale-up.
2. Platform must provide cache metrics and correlation-aware observability.
3. Product must finalize public-identity privacy rules for leaderboard exposure.

### Risks to Plan

- Risk: delayed blocker resolution compresses QA implementation windows.
  - Impact: reduced pre-release confidence.
  - Contingency: prioritize P0/P1 coverage and defer lower-priority suites.
