---
title: "Product Brief Distillate: bmad"
type: llm-distillate
source: "product-brief-bmad.md"
created: "2026-04-23T10:31:43.1455667+03:00"
updated: "2026-05-04T12:40:33.5678113+03:00"
purpose: "Token-efficient context for downstream PRD creation"
---

# Product Brief Distillate: Task Tracker

## Product Intent Snapshot
- Product concept: Web-first task tracker that couples practical task execution with explicit motivation loops.
- Core user promise: Increase completion consistency through immediate reinforcement (XP, streak continuity, progress movement).
- Primary behavior target: Reduce execution decay after initial signup by making progress visible and emotionally meaningful.
- Strategic framing: "Execution-first task management with measurable momentum."
- Release posture: Planning artifacts indicate implementation-ready baseline for continued delivery.

## Requirements Hints
- PRD coverage signal: 48 functional requirements are defined and mapped 100% to epics.
- NFR coverage signal: 31 non-functional requirements define performance, security, reliability, accessibility, and integration expectations.
- Functional requirement hint: task create/edit/delete/complete must remain low-friction and ownership-scoped.
- Functional requirement hint: completion must trigger deterministic and idempotent XP updates.
- Functional requirement hint: streak engine must apply explicit timezone and daily-boundary policy.
- Functional requirement hint: leaderboards must expose privacy-safe identity fields only and preserve deterministic rank/tie-break behavior.
- Functional requirement hint: global stats page must expose total tasks created/completed and remain responsive under growth.
- Functional requirement hint: user/admin/support role capabilities must be enforced server-side and audited for privileged actions.
- UX requirement hint: motivational overlays must not slow down the core task loop on mobile or desktop.
- Analytics requirement hint: capture onboarding funnel, completion milestones, streak milestones, leaderboard engagement, and dispute-resolution traces.

## Technical Context
- Platform direction: ASP.NET Core backend + Angular frontend.
- Data platform preference: SQL Server (SQL Server 2022+ / Azure SQL Database) via EF Core SQL Server provider.
- Architecture baseline: modular monolith with domain boundaries for identity, tasks, progression, rankings/statistics, notifications, integrations, and operations.
- API baseline: REST JSON under /api/v1 with standardized Problem Details error contracts.
- Security baseline: JWT auth, least-privilege role policies, server-side ownership checks, immutable audit trails for admin/support actions.
- Consistency baseline: idempotent completion processing, dedup keys, and deterministic event handling under retries/reconnects.
- Performance baseline: cache-first read models for leaderboard/statistics with explicit invalidation tied to committed completion events.
- Time semantics baseline: UTC event storage + user timezone projection for streak boundaries.

## Detailed User Scenarios
- Scenario: Student/professional starts day planning in minutes, completes first task quickly, and receives immediate momentum signal.
- Scenario: Freelancer with irregular schedule misses cutoff and needs recovery UX that restores agency instead of causing churn.
- Scenario: User compares progress in streak and completion leaderboards while retaining control over public participation.
- Scenario: Support role investigates "missing XP" or "incorrect streak" complaints using traceable event timeline and deterministic rule inspection.
- Scenario: Admin role detects suspicious completion patterns and applies moderation without corrupting ranking trust.
- Scenario: Integration consumer creates tasks via API while preserving same ownership, validation, and progression rules as UI.

## Competitive Intelligence
- Asana signal: broad team workflow depth; not the target for personal execution-first MVP.
- Todoist signal: strongest mainstream positioning on frictionless capture and cross-platform sync.
- TickTick signal: all-in-one productivity suite with extensive feature surface (calendar, habit, focus, reminders).
- Habitica signal: deep gamification and community mechanics validate motivation-as-product pattern.
- Positioning implication: win through a cleaner completion loop with stronger immediate reinforcement and trust-preserving social signals.

## Scope Signals
- In MVP: authentication/session lifecycle, profile/settings baseline, ownership-scoped task lifecycle, deterministic XP + streak processing, leaderboards, global stats, essential email flows, role-policy baseline.
- Out of MVP: native mobile apps, deep enterprise workspace controls, advanced AI planner/recommender, complex PM dependencies/resource planning.
- Scope discipline signal: protect completion reliability and motivation quality before expanding horizontal feature surface.

## Rejected Ideas and Deferred Directions
- Rejected/deferred: enterprise PM depth is intentionally excluded to avoid product dilution.
- Rejected/deferred: AI-heavy recommendations postponed until behavior loop and retention are validated.
- Rejected/deferred: native mobile postponed until web loop proves repeatable value.
- Deferred architecture: multi-region active-active and external event bus extraction beyond MVP.

## Risks to Carry into PRD
- Risk: novelty decay if progression mechanics are not tuned with cohort data.
- Risk: discouragement from social comparison without strong privacy/personal-best framing.
- Risk: trust erosion if XP/streak outcomes are perceived as unfair or inconsistent.
- Risk: leaderboard abuse from low-effort or scripted completions.
- Risk: implementation drift from deterministic rules under retry/reconnect edge cases.

## Open Questions
- XP policy: flat vs weighted vs hybrid; anti-inflation controls and fairness perception.
- Streak policy: completion threshold, grace-window behavior, and user-visible timezone semantics.
- Privacy policy: opt-in defaults, aliasing strategy, and participation controls for public ranks.
- Integrity policy: anti-gaming thresholds, automated flags, and manual moderation playbook boundaries.
- Onboarding policy: shortest path from signup to first completed task and first multi-day streak.
- Ranking policy: deterministic tie-break order and reconciliation process for disputes.

## Suggested PRD Starting Assumptions
- Assumption: MVP optimizes for single-user momentum with optional social comparison, not collaborative project operations.
- Assumption: first completed task, first 3-day streak, and first leaderboard interaction are primary early value moments.
- Assumption: leaderboard/global stats read paths must remain cache-optimized under growth.
- Assumption: full event traceability from day one is mandatory for support explainability and trust protection.
