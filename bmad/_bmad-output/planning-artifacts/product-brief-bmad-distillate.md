---
title: "Product Brief Distillate: bmad"
type: llm-distillate
source: "product-brief-bmad.md"
created: "2026-04-23T10:31:43.1455667+03:00"
purpose: "Token-efficient context for downstream PRD creation"
---

# Product Brief Distillate: Task Tracker

## Product Intent Snapshot
- Product concept: A web-based Task Tracker that combines lightweight task management with gamified motivation loops.
- Core user promise: Help people complete tasks more consistently by making progress visible and rewarding.
- Primary behavior target: Increase task completion consistency, not just task capture.
- Strategic framing: "Simple task management + motivating game loop" for individuals, not enterprise workflow complexity.

## Requirements Hints
- Functional requirement hint: Users must create, edit, delete, and complete tasks with low friction.
- Functional requirement hint: Completing a task must award XP immediately and visibly.
- Functional requirement hint: System must track streak continuity over time and show it in user-facing UI.
- Functional requirement hint: System must provide global leaderboard views sorted by streak and by completed task count.
- Functional requirement hint: System must provide a global statistics page containing total tasks created and total tasks completed across all users.
- Functional requirement hint: Authentication and user accounts are required in MVP.
- UX requirement hint: Core task flow should remain fast and uncluttered despite gamification features.
- Analytics requirement hint: Instrument funnel events for create-first-task, complete-first-task, streak milestones, leaderboard views.

## Technical Context
- Platform direction provided by user: ASP.NET backend with Angular frontend.
- Delivery shape: Web app first; mobile native explicitly deferred beyond MVP.
- Architecture implication: Backend should expose APIs for tasks, XP transactions, streak computation, leaderboard queries, and global counters.
- Architecture implication: Need durable event/state model for completion events to support anti-gaming checks and analytics.
- Data integrity implication: Leaderboards require deterministic ranking rules and tie-break strategy.
- Time logic implication: Streaks require explicit timezone policy, daily boundary rules, and potential grace-window handling.

## Detailed User Scenarios
- Scenario: User captures multiple daily tasks in the morning and checks them off through the day, seeing XP rise in real time.
- Scenario: User misses tasks for several days today with existing tools and wants visible momentum signals to avoid drop-off.
- Scenario: User compares progress to others on streak and completed-task leaderboards for accountability and motivation.
- Scenario: User opens statistics page to see macro community progress, reinforcing a sense of participation and social proof.
- Scenario: Friendly-competition user cohort (students/freelancers/peers) uses rankings as external motivation for consistency.

## Competitive Intelligence
- Asana positioning signal: Strong at team workflow/project operations; likely over-scoped for this product's personal-focus MVP.
- Todoist positioning signal: Strong frictionless capture and organization; includes productivity trend visualization.
- TickTick positioning signal: Broad all-in-one productivity toolkit, including statistics and habit/focus adjacent features.
- Habitica positioning signal: Demonstrates that gamification (rewards, progress mechanics) can sustain engagement.
- Product implication: Differentiate through focused, clean task flow plus meaningful progression and social comparison, not feature breadth.

## Scope Signals
- In MVP: Accounts/auth, task CRUD, completion flow, XP, streaks, leaderboards (streak + completions), global counters page.
- Out MVP: Native mobile apps, team admin/workspace complexity, advanced AI recommendations, enterprise PM features, monetization experiments.
- Scope discipline signal: Prefer behavior-changing core loop quality over expansion of feature surface area.

## Rejected Ideas and Deferred Directions
- Deferred direction: Enterprise-oriented project management depth (dependencies/resource planning/portfolio layers) is intentionally excluded to preserve product focus.
- Deferred direction: AI-heavy prioritization and recommendation features are delayed until core behavior loop proves retention value.
- Deferred direction: Native mobile apps are postponed to avoid splitting early implementation capacity.
- Not provided in source discovery: No user-supplied prior documents, research artifacts, or legacy constraints to preserve.

## Risks to Carry into PRD
- Risk: Gamification novelty decay could reduce long-term engagement if progression lacks depth.
- Risk: Public ranking can demotivate users with low scores without private/personalized framing options.
- Risk: Feature creep may dilute core completion loop and delay delivery.
- Risk: Leaderboard abuse (task spam, low-effort completions) can undermine trust without anti-gaming controls.

## Open Questions
- XP model choice: Flat XP vs weighted by task difficulty/importance, and how to prevent reward inflation.
- Streak model choice: Minimum daily completion threshold, grace period policy, and timezone source of truth.
- Privacy model choice: Global-only leaderboard visibility vs opt-in participation and anonymization options.
- Integrity model choice: Anti-cheat heuristics, suspicious-activity flags, and moderation/remediation process.
- Onboarding model choice: Fastest path from signup to first meaningful completion and first streak moment.
- Ranking model choice: Tie-break ordering (latest completion time, total XP, account age, etc.).

## Suggested PRD Starting Assumptions
- Assumption: MVP should optimize for single-user daily use with optional public competition rather than collaborative team workflows.
- Assumption: "First completed task" and "first 3-day streak" are primary early value moments to design around.
- Assumption: Leaderboards and global stats should remain read-optimized and cached to keep UI responsive.
- Assumption: Event logging from day one is mandatory to support retention tuning and reward-system balancing.
