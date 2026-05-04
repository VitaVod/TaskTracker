---
stepsCompleted:
  - 1
  - 2
  - 3
  - 4
status: complete
completedAt: 2026-04-24
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/planning-artifacts/ux-design-specification.md
---

# bmad - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for bmad, decomposing requirements from PRD, Architecture, and UX design specification into implementable stories sized for a single developer session.

## Requirements Inventory

### Functional Requirements

FR1: Visitors can create a user account.
FR2: Registered users can sign in and sign out of the product.
FR3: Users can manage core profile information used by the product.
FR4: Users can control settings related to their account experience.
FR5: Users can access only the data and capabilities permitted by their role.
FR6: Administrators can access administrative capabilities unavailable to standard users.
FR7: Support users can access troubleshooting capabilities appropriate to their role.
FR8: Users can create tasks associated with their own account.
FR9: Users can view their active and completed tasks.
FR10: Users can edit tasks they own.
FR11: Users can delete tasks they own.
FR12: Users can mark tasks as complete.
FR13: Users can organize tasks using basic organizational attributes supported by the product.
FR14: Users can distinguish between incomplete and completed task states.
FR15: Users can receive XP when completing eligible tasks.
FR16: Users can view their current XP total and related progress indicators.
FR17: Users can view their current streak status.
FR18: The system can determine whether a user's streak continues, resets, or restarts based on task completion activity.
FR19: Users can understand when a task completion affects XP and streak outcomes.
FR20: Users can view historical or cumulative progress signals that reinforce continued usage.
FR21: Users can view a leaderboard ranked by streak performance.
FR22: Users can view a leaderboard ranked by completed task count.
FR23: The system can display approved public identity information for leaderboard participants.
FR24: Users can view global statistics for total tasks created across the platform.
FR25: Users can view global statistics for total tasks completed across the platform.
FR26: The system can update shared progress views to reflect relevant new activity.
FR27: Users can access only their own tasks, XP data, streak data, and private profile information.
FR28: Users can participate in public ranking features according to the product's privacy rules.
FR29: The system can restrict administrative and support capabilities to authorized internal roles only.
FR30: The system can record sensitive administrative and support actions for audit purposes.
FR31: Administrators can review suspicious activity affecting leaderboard integrity.
FR32: Administrators can apply moderation actions to protect ranking fairness.
FR33: Support users can inspect user account state relevant to XP, streak, and task troubleshooting.
FR34: Support users can review user event history needed to explain unexpected progress outcomes.
FR35: Internal roles can investigate disputes related to leaderboard position, XP allocation, or streak behavior.
FR36: Authorized integrations can create or synchronize tasks on behalf of a user when permitted.
FR37: Integration-created tasks can be associated with a single authorized user identity.
FR38: External access paths can follow the same ownership, authorization, and validation rules as first-party product flows.
FR39: Users can recover from missed streaks through product-supported re-engagement paths.
FR40: Users can understand the outcome of missed activity periods on their progress state.
FR41: The product can surface motivational progress feedback tied to meaningful user actions.
FR42: Users can complete the core value loop of planning work, finishing work, and seeing visible reward.
FR43: Registered users can request password recovery through email.
FR44: Registered users can receive account-related email notifications required for secure account access and recovery.
FR45: Users can receive reminder emails about pending or incomplete tasks.
FR46: Users can manage email notification preferences for supported notification types.
FR47: The system can send task reminder emails based on user notification preferences.
FR48: The system can send transactional emails for password recovery and other critical account events.

### NonFunctional Requirements

NFR1: Authentication actions should complete within 2 seconds under normal conditions.
NFR2: Core dashboard load should complete within 2 seconds under normal conditions.
NFR3: Task create, edit, delete, and complete actions should reflect successful results within 1 second after server acknowledgment.
NFR4: XP and streak feedback should appear within 1 second of successful task completion.
NFR5: Leaderboard and global statistics views should load within 3 seconds.
NFR6: Real-time updates must not create inconsistent or duplicate visible state.
NFR7: All authenticated endpoints require server-side authentication and authorization checks.
NFR8: All user data must be encrypted in transit and protected data encrypted at rest.
NFR9: Least-privilege access must be enforced for user, admin, support, and integration roles.
NFR10: Sensitive administrative and support actions must be auditable.
NFR11: Session/token flows must support expiration, revocation, and secure renewal.
NFR12: Password recovery must use time-limited, single-use secure links.
NFR13: System should scale to at least 10x active-user growth without fundamental redesign.
NFR14: Shared views must remain responsive through caching/query optimization.
NFR15: Product must target WCAG 2.1 AA and keyboard operability.
NFR16: External integrations must preserve auth, ownership, validation, and deterministic retry handling.
NFR17: Core task/progress state must remain reliable and consistent through retries/reconnects.
NFR18: Critical transactional emails must use monitored delivery with retry handling.

### Additional Requirements

- Starter template is mandatory: Angular CLI frontend plus ASP.NET Core .NET 9 Web API backend with SQL Server (Epic 1 Story 1).
- API contract standard must use versioned REST (`/api/v1`) and Problem Details with stable app error codes and trace ID.
- Completion and reward commands require idempotency keys and deduplication behavior.
- Time semantics require UTC event storage with user-timezone projection for streak boundaries.
- Leaderboard/statistics read paths require caching plus deterministic invalidation after completion commits.
- Authorization must be server-side ownership checks on every protected resource.
- Privileged admin/support actions require immutable audit entries with actor, target, reason, timestamp, correlation ID.
- Architecture style is modular monolith with clear Api/Application/Domain/Infrastructure boundaries.
- Event instrumentation and observability are required for activation, completion, streak, and leaderboard usage.
- Public identity in leaderboard must be privacy-safe and policy-driven.

### UX Design Requirements

UX-DR1: Implement tokenized visual system (color, spacing, typography, motion) and apply consistently across app surfaces.
UX-DR2: Ensure completion feedback pattern gives immediate local confirmation (toast/inline state) for XP and streak outcomes.
UX-DR3: Implement dashboard-first IA with persistent primary navigation and mobile-optimized navigation pattern.
UX-DR4: Provide Streak Continuity Card component with at-risk state and next-action guidance.
UX-DR5: Provide XP Feedback component/notification with deterministic messaging for rewarded and non-rewarded actions.
UX-DR6: Provide Momentum Summary panel for daily/weekly trend visibility.
UX-DR7: Provide Leaderboard row component with rank, movement delta, and privacy-safe identity display.
UX-DR8: Provide Recovery Prompt module for missed-day explanation and restart action.
UX-DR9: Ensure task interactions and key controls meet mobile touch target and responsive breakpoint behavior.
UX-DR10: Enforce accessibility patterns: focus-visible, keyboard flow, screen-reader announcements for dynamic status changes.
UX-DR11: Implement empty, loading, and error states with action-oriented guidance and non-blocking recovery.
UX-DR12: Preserve deterministic focus and form validation behaviors with clear, inline corrective guidance.

### FR Coverage Map

FR1: Epic 1 - User can register account.
FR2: Epic 1 - User can sign in/out.
FR3: Epic 1 - User can manage profile.
FR4: Epic 1 - User can manage account settings.
FR5: Epic 1 - Role-based access for all users.
FR6: Epic 1 - Admin role capability baseline.
FR7: Epic 1 - Support role capability baseline.
FR8: Epic 2 - Create tasks.
FR9: Epic 2 - View active/completed tasks.
FR10: Epic 2 - Edit own tasks.
FR11: Epic 2 - Delete own tasks.
FR12: Epic 2 - Complete tasks.
FR13: Epic 2 - Organize tasks with basic attributes.
FR14: Epic 2 - Distinguish task states.
FR15: Epic 3 - Award XP on eligible completion.
FR16: Epic 3 - Display XP and progress indicators.
FR17: Epic 3 - Display streak status.
FR18: Epic 3 - Determine streak continue/reset/restart.
FR19: Epic 3 - Explain completion impact on XP/streak.
FR20: Epic 3 - Show historical/cumulative progress.
FR21: Epic 4 - Streak leaderboard.
FR22: Epic 4 - Completed-task leaderboard.
FR23: Epic 4 - Privacy-safe public identity display.
FR24: Epic 4 - Global total-created statistic.
FR25: Epic 4 - Global total-completed statistic.
FR26: Epic 4 - Shared views updated with activity.
FR27: Epic 1 - Ownership-based private data access.
FR28: Epic 4 - Participation by privacy rules.
FR29: Epic 6 - Restrict internal capabilities to authorized roles.
FR30: Epic 6 - Record sensitive internal actions.
FR31: Epic 6 - Admin suspicious activity review.
FR32: Epic 6 - Admin moderation actions.
FR33: Epic 6 - Support account-state inspection.
FR34: Epic 6 - Support event-history review.
FR35: Epic 6 - Internal dispute investigation workflows.
FR36: Epic 7 - Authorized integrations create/sync tasks.
FR37: Epic 7 - Integration tasks tied to one authorized user.
FR38: Epic 7 - Integration paths follow same validation/authz model.
FR39: Epic 5 - Recovery flow after missed streak.
FR40: Epic 5 - Explain missed activity outcomes.
FR41: Epic 5 - Motivational progress feedback tied to actions.
FR42: Epic 3 - Core value loop visible from completion to reward.
FR43: Epic 1 - Password recovery request.
FR44: Epic 1 - Account-related transactional email baseline.
FR45: Epic 5 - Reminder emails for pending/incomplete tasks.
FR46: Epic 5 - Notification preference management.
FR47: Epic 5 - Preference-aware reminder delivery.
FR48: Epic 5 - Transactional emails for critical account events.

## Epic List

### Epic 1: Secure Platform Foundation and Identity
Deliver a production-ready starter architecture and secure identity foundation so users can register, authenticate, manage profiles/settings, and recover accounts with correct role and ownership boundaries.
**FRs covered:** FR1, FR2, FR3, FR4, FR5, FR6, FR7, FR27, FR43, FR44

### Epic 2: Personal Task Lifecycle
Enable users to manage their personal work reliably by creating, organizing, updating, completing, and deleting tasks with clear active/completed state behavior.
**FRs covered:** FR8, FR9, FR10, FR11, FR12, FR13, FR14

### Epic 3: Progression Engine and Momentum Visibility
Turn completion into motivation by implementing deterministic XP/streak processing and progress visibility that makes the core value loop immediate and trustworthy.
**FRs covered:** FR15, FR16, FR17, FR18, FR19, FR20, FR42

### Epic 4: Social Momentum and Shared Metrics
Provide leaderboard and ecosystem visibility so users can compare momentum and observe platform-wide activity while honoring privacy and freshness expectations.
**FRs covered:** FR21, FR22, FR23, FR24, FR25, FR26, FR28

### Epic 5: Engagement, Recovery, and Notification Experience
Keep users consistent over time by supporting reminders, recovery journeys, and explanatory feedback for missed activity and critical account events.
**FRs covered:** FR39, FR40, FR41, FR45, FR46, FR47, FR48

### Epic 6: Trust, Moderation, and Support Operations
Protect fairness and user trust through internal admin/support tools, strict privileged access, and complete auditability for sensitive actions.
**FRs covered:** FR29, FR30, FR31, FR32, FR33, FR34, FR35

### Epic 7: Integration Access and External Consistency
Allow approved external systems to create/sync tasks safely while preserving the same ownership, authorization, validation, and idempotency guarantees as first-party flows.
**FRs covered:** FR36, FR37, FR38

## Epic 1: Secure Platform Foundation and Identity

Establish starter projects and secure identity/authorization baseline with role-aware access and account lifecycle flows.

### Story 1.1: Initialize Solution from Selected Starter Template

As a developer,
I want to scaffold the Angular web app and ASP.NET Core API with SQL Server wiring,
So that all later stories build on the approved architecture baseline.

**Acceptance Criteria:**

**Given** an empty repository root
**When** starter commands are run for Angular and ASP.NET Core projects and solution wiring
**Then** `task-tracker-web` and `task-tracker-api` are created with buildable defaults
**And** SQL Server EF Core provider and migration scaffolding baseline are configured.

### Story 1.2: Implement User Registration and Login

As a visitor,
I want to create an account and log in securely,
So that I can access my personal task workspace.

**Acceptance Criteria:**

**Given** a visitor with valid credentials
**When** they submit registration
**Then** a user account is created and confirmation response is returned
**And** password policies and duplicate-email validation are enforced.

**Given** a registered user
**When** they submit valid login credentials
**Then** the API issues access and refresh tokens
**And** failed attempts return standardized Problem Details errors.

### Story 1.3: Implement Secure Session Lifecycle and Logout

As an authenticated user,
I want secure token renewal and logout,
So that my session remains safe and controllable.

**Acceptance Criteria:**

**Given** a valid refresh token
**When** token refresh is requested
**Then** a new access token is issued and old token state is rotated/revoked per policy
**And** token expiration/revocation behaviors are auditable.

**Given** an authenticated session
**When** logout is requested
**Then** active refresh token is revoked
**And** subsequent API calls with old session material are rejected.

### Story 1.4: Build Profile and Account Settings Management

As a user,
I want to update profile and account preferences,
So that my identity and account experience match my needs.

**Acceptance Criteria:**

**Given** an authenticated user
**When** they update profile/settings fields
**Then** only allowed fields are changed and persisted
**And** validation errors are returned with field-level detail.

### Story 1.5: Implement Role Policies and Ownership Authorization Baseline

As a platform owner,
I want server-side role and ownership enforcement,
So that users and internal roles can access only permitted capabilities.

**Acceptance Criteria:**

**Given** any protected endpoint
**When** the request is evaluated
**Then** role policy and ownership checks are applied server-side
**And** unauthorized and forbidden responses use consistent error contracts.

**Given** admin or support routes
**When** a standard user attempts access
**Then** access is denied
**And** the denial is logged with trace context.

### Story 1.6: Implement Password Recovery and Critical Transactional Email

As a user,
I want to recover account access through secure email flows,
So that I can regain access when credentials are lost.

**Acceptance Criteria:**

**Given** a registered email
**When** password recovery is requested
**Then** a time-limited, single-use recovery link is issued
**And** delivery is routed through transactional email service with retry policy.

**Given** a recovery link is reused or expired
**When** reset is attempted
**Then** reset is rejected with explicit recovery guidance
**And** security events are logged.

## Epic 2: Personal Task Lifecycle

Deliver complete user task management with clear state transitions and responsive UX on desktop and mobile.

### Story 2.1: Create Task Domain and API Contracts

As an authenticated user,
I want to create tasks with essential attributes,
So that I can capture work items quickly.

**Acceptance Criteria:**

**Given** valid task input
**When** create task is submitted
**Then** task is stored under requesting user ownership
**And** response includes normalized task payload for immediate UI rendering.

**Given** invalid task input
**When** create task is submitted
**Then** validation errors return in Problem Details format
**And** no task is created.

### Story 2.2: Build Task List Views for Active and Completed Items

As a user,
I want to view active and completed tasks distinctly,
So that I can focus on what remains and review what is done.

**Acceptance Criteria:**

**Given** tasks exist in mixed states
**When** tasks are requested
**Then** API and UI expose active/completed filters
**And** state labels are clear and accessible.

### Story 2.3: Implement Task Update and Organizational Attributes

As a user,
I want to edit task details and organization fields,
So that task planning stays current and manageable.

**Acceptance Criteria:**

**Given** a task owned by the user
**When** edit request updates title, due date, or category/priority
**Then** changes are persisted and returned
**And** updates to tasks not owned by requester are denied.

### Story 2.4: Implement Task Completion Toggle with Deterministic State

As a user,
I want to mark tasks complete and incomplete as allowed by policy,
So that my task status accurately reflects reality.

**Acceptance Criteria:**

**Given** a valid owned task
**When** completion action is sent
**Then** task state transitions deterministically and emits completion event for progression engine
**And** duplicate submissions do not create conflicting task state.

### Story 2.5: Implement Task Deletion and Safe UX Confirmation

As a user,
I want to delete tasks I no longer need,
So that my task list remains clean and relevant.

**Acceptance Criteria:**

**Given** an owned task
**When** delete is confirmed
**Then** task is deleted (or soft-deleted per policy) and removed from active views
**And** deletion of another user's task is forbidden.

### Story 2.6: Build Task UI States for Empty, Loading, and Error Conditions

As a user,
I want clear non-happy-path task states,
So that I always know what to do next.

**Acceptance Criteria:**

**Given** no tasks exist
**When** tasks page loads
**Then** an action-oriented empty state is shown
**And** create-task action is prominent.

**Given** load or save failures
**When** errors occur
**Then** recovery actions are shown
**And** keyboard and screen-reader paths remain usable.

## Epic 3: Progression Engine and Momentum Visibility

Implement deterministic progression processing and UX reinforcement so completion reliably translates into visible momentum.

### Story 3.1: Build XP Ledger and Idempotent Completion Processing

As a user,
I want XP granted exactly once for an eligible completion,
So that progress feels fair and trustworthy.

**Acceptance Criteria:**

**Given** a task completion event with idempotency key
**When** progression command runs
**Then** XP ledger entry is created at most once per eligible event
**And** retries return consistent result without duplicate XP grants.

### Story 3.2: Implement Streak Rule Engine with Timezone Policy

As a user,
I want streak outcomes computed accurately for my local day boundaries,
So that streak continuity feels predictable.

**Acceptance Criteria:**

**Given** user timezone and historical completion events
**When** streak evaluation runs
**Then** result is continue, reset, or restart according to deterministic policy
**And** UTC storage plus timezone projection is used for calculations.

### Story 3.3: Expose Progress APIs for XP, Streak, and Trend Snapshots

As a user,
I want to view current progression status and trend data,
So that I can monitor momentum over time.

**Acceptance Criteria:**

**Given** authenticated progress request
**When** XP/streak/summary endpoints are called
**Then** current totals and trend snapshots are returned with bounded latency
**And** ownership checks prevent cross-user access.

### Story 3.4: Build Dashboard Progress Components and Feedback UI

As a user,
I want immediate feedback after completion,
So that I can see that the action counted.

**Acceptance Criteria:**

**Given** a successful task completion
**When** UI receives completion response
**Then** XP Feedback and Streak Continuity components update within 1 second target
**And** announcements are available for assistive technologies.

### Story 3.5: Implement Momentum Summary and Historical Progress View

As a user,
I want to see daily/weekly completion trend context,
So that I stay motivated beyond individual task events.

**Acceptance Criteria:**

**Given** historical completion data exists
**When** momentum view loads
**Then** cumulative and recent trend metrics are displayed clearly
**And** visual indicators do not rely on color alone.

## Epic 4: Social Momentum and Shared Metrics

Deliver leaderboard and global metrics views that are responsive, privacy-safe, and consistently refreshed.

### Story 4.1: Implement Streak and Completed-Task Leaderboard Read Models

As a user,
I want to compare ranking by streak and completed tasks,
So that I can benchmark my momentum.

**Acceptance Criteria:**

**Given** ranking requests for supported leaderboard types
**When** leaderboard endpoints are called
**Then** deterministic rank ordering and tie-break rules are applied
**And** responses are paginated and performance-aware.

### Story 4.2: Implement Privacy-Safe Public Identity and Participation Controls

As a user,
I want leaderboard visibility aligned with privacy policy,
So that I can participate comfortably.

**Acceptance Criteria:**

**Given** leaderboard participant profiles
**When** leaderboard payload is generated
**Then** only approved public identity fields are exposed
**And** participation respects privacy settings and policy rules.

### Story 4.3: Build Global Statistics Endpoints and UI Panels

As a user,
I want platform-wide totals for created and completed tasks,
So that I can see ecosystem activity.

**Acceptance Criteria:**

**Given** stats request
**When** global stats endpoint is called
**Then** total created and total completed counters are returned
**And** UI renders stats panels with loading/error states.

### Story 4.4: Add Cache and Invalidation Strategy for Shared Views

As a platform owner,
I want leaderboard/stats reads to stay fast and fresh,
So that shared views scale under load.

**Acceptance Criteria:**

**Given** completion commits that affect shared views
**When** cache invalidation triggers
**Then** leaderboard and stats cache entries are refreshed within defined freshness window
**And** stale/duplicate view anomalies are detectable by telemetry.

### Story 4.5: Build Leaderboard UX Components and Responsive Behaviors

As a user,
I want leaderboard screens that are readable and motivating on any device,
So that social comparison remains useful.

**Acceptance Criteria:**

**Given** desktop and mobile breakpoints
**When** leaderboard views render
**Then** Leaderboard Momentum Row and movement indicators remain legible and accessible
**And** keyboard navigation and assistive labels are complete.

## Epic 5: Engagement, Recovery, and Notification Experience

Strengthen consistency through reminders, recovery messaging, and user-controlled notification behavior.

### Story 5.1: Implement Notification Preferences Domain and API

As a user,
I want to configure reminder and account-notification preferences,
So that communication matches my needs.

**Acceptance Criteria:**

**Given** authenticated user preference changes
**When** preference API is called
**Then** preferences are persisted per user
**And** defaults and validation rules are enforced.

### Story 5.2: Implement Reminder Email Pipeline for Pending Tasks

As a user,
I want reminder emails for pending or incomplete tasks,
So that I stay on track.

**Acceptance Criteria:**

**Given** reminder job execution and eligible pending tasks
**When** reminder processing runs
**Then** emails are sent according to user preferences and schedule rules
**And** delivery failures are retried and logged.

### Story 5.3: Build Missed-Day Recovery Experience and Guidance

As a user,
I want a clear recovery path after missing a day,
So that I can re-engage quickly.

**Acceptance Criteria:**

**Given** a missed streak day is detected
**When** user returns to dashboard
**Then** Recovery Prompt module explains impact and next-step action
**And** messaging is supportive and deterministic.

### Story 5.4: Implement Progress Explanation Messages for Outcome Transparency

As a user,
I want to understand why my streak or XP changed,
So that I trust system behavior.

**Acceptance Criteria:**

**Given** completion and streak events
**When** outcome explanation is requested or displayed
**Then** clear reason text links action to resulting XP/streak state
**And** explanations align exactly with backend rules.

### Story 5.5: Integrate Transactional Notification Flows with Account Events

As a user,
I want critical account event notifications delivered reliably,
So that I can act on security-related events quickly.

**Acceptance Criteria:**

**Given** account-critical events (password reset, security-related account actions)
**When** transactional pipeline executes
**Then** required emails are sent with monitored status and retry behavior
**And** failures surface to operational logs/alerts.

## Epic 6: Trust, Moderation, and Support Operations

Enable internal teams to preserve fairness and resolve user issues with strict access controls and forensic visibility.

### Story 6.1: Build Admin Suspicious-Activity Review Workspace

As an administrator,
I want to review abnormal completion/ranking patterns,
So that I can detect potential abuse.

**Acceptance Criteria:**

**Given** ranking and activity anomaly signals
**When** admin review page loads
**Then** suspicious cases are listed with relevant context
**And** access is restricted to admin role.

### Story 6.2: Implement Moderation Actions with Safety Guards

As an administrator,
I want to apply moderation actions to protect leaderboard integrity,
So that rankings remain fair.

**Acceptance Criteria:**

**Given** a reviewed suspicious case
**When** moderation action is executed
**Then** ranking correction/flag action is applied under policy rules
**And** destructive operations require explicit confirmation.

### Story 6.3: Build Support Diagnostic View for User Progress Disputes

As a support user,
I want read-only visibility into user task/progress state,
So that I can resolve reported issues quickly.

**Acceptance Criteria:**

**Given** a support investigation request
**When** support view loads for a user
**Then** relevant account/task/xp/streak snapshots are displayed read-only
**And** support role cannot mutate protected user data.

### Story 6.4: Implement Event Timeline and Correlation-Based Troubleshooting

As a support user,
I want an event timeline with trace context,
So that I can explain unexpected outcomes.

**Acceptance Criteria:**

**Given** a dispute scenario
**When** timeline query runs
**Then** ordered events with timestamps, rule outcomes, and trace/correlation IDs are shown
**And** timeline can be filtered by event type/date.

### Story 6.5: Implement Immutable Audit Logging for Privileged Actions

As a compliance-minded operator,
I want privileged actions captured immutably,
So that accountability is maintained.

**Acceptance Criteria:**

**Given** admin/support privileged actions
**When** action is completed
**Then** audit record stores actor, target, action, reason, timestamp, and correlation ID
**And** audit records are queryable and tamper-resistant per policy.

## Epic 7: Integration Access and External Consistency

Enable secure external task ingestion and synchronization with identical validation and ownership semantics as internal flows.

### Story 7.1: Implement Integration Authentication and Scoped Credentials

As an integration partner,
I want authenticated, scoped access,
So that external automation can operate safely.

**Acceptance Criteria:**

**Given** integration credentials with scope
**When** integration API call is made
**Then** request is authorized only for granted scopes
**And** unauthorized scopes return consistent forbidden errors.

### Story 7.2: Implement Task Create/Sync Endpoint for Integrations

As an integration partner,
I want to create or sync tasks for an authorized user,
So that external planning systems can feed Task Tracker.

**Acceptance Criteria:**

**Given** valid integration payload mapped to a single authorized user
**When** create/sync operation executes
**Then** tasks are created or updated under that user ownership only
**And** payload validation uses same domain rules as first-party task flows.

### Story 7.3: Implement Idempotent Retry Handling for Integration Events

As a platform owner,
I want deterministic retry behavior for integration requests,
So that repeated events do not corrupt state.

**Acceptance Criteria:**

**Given** duplicate or retried integration events
**When** processing occurs
**Then** idempotency key handling prevents duplicate mutations
**And** responses indicate prior success where applicable.

### Story 7.4: Add Integration Observability and Failure Recovery

As an operations team member,
I want visibility into integration health,
So that failures are diagnosed and recovered quickly.

**Acceptance Criteria:**

**Given** integration processing activity
**When** telemetry is collected
**Then** success/failure rates, retries, and error classes are observable
**And** failure events include enough context for support/admin troubleshooting.

## Epic 8: Progression Integrity, Momentum UX, and Public Profiles

Close progression trust gaps and deliver the next engagement wave by hardening XP/streak rules, improving momentum usability, and expanding profile/privacy-aware social features.

### Story 8.1: Enforce Progression Integrity for Task State and Deletion Rules

As a user,
I want XP and completed counters to remain fair and deterministic,
So that my progress cannot be lost by destructive or inconsistent task transitions.

**Acceptance Criteria:**

**Given** a completed task
**When** delete is requested
**Then** deletion is rejected by business rules
**And** user is guided to archive/hide instead.

**Given** a task transitions from completed to active
**When** transition succeeds
**Then** awarded XP is compensated and completed-task counter is decremented exactly once
**And** event processing remains idempotent.

### Story 8.2: Extend Task Model with Difficulty and Planning Metadata

As a user,
I want to classify tasks by difficulty, energy, and context,
So that planning is more realistic and rewards match effort.

**Acceptance Criteria:**

**Given** task create or update
**When** metadata is submitted
**Then** difficulty, energy level, context tag, effort points, and predicted duration are validated and stored.

**Given** task completion
**When** XP is awarded
**Then** difficulty mapping applies deterministically (easy 10, medium 20, hard 30)
**And** repeated completion events do not double-award XP.

### Story 8.3: Build Level Thresholds and Multi-Band XP Progress Bar

As a user,
I want to see levels and a color-banded XP bar,
So that long-term progression is clear and motivating.

**Acceptance Criteria:**

**Given** current XP and thresholds
**When** dashboard progress renders
**Then** current level, next threshold, and percent-to-next-level are shown.

**Given** level transitions
**When** user reaches levels 3, 5, 10, 20, 30, and 50
**Then** configured color bands are applied accessibly
**And** non-color cues still communicate status.

### Story 8.4: Add Weekly Recovery Token and Near-Miss Streak Nudges

As a user,
I want occasional streak protection and timely nudges,
So that a single missed day does not fully break momentum.

**Acceptance Criteria:**

**Given** streak evaluation in user timezone
**When** a missed day occurs and weekly token is available
**Then** one recovery token is consumed and streak continuity is preserved
**And** token lifecycle is auditable.

**Given** user is one task short of preserving streak tier
**When** nudge window is reached and preferences allow
**Then** a near-miss reminder is sent at most once per local day.

### Story 8.5: Redesign Momentum Summary with Daily Detail and Month Heatmap

As a user,
I want a readable momentum overview with drill-down,
So that I can understand daily progress trends and act on them.

**Acceptance Criteria:**

**Given** momentum summary loads
**When** historical data exists
**Then** responsive card/list presentation replaces unwrapped table layout
**And** each summary item links to day-level details.

**Given** monthly activity visualization
**When** heatmap renders
**Then** last-month day cells reflect activity intensity
**And** selected day opens detailed statistics view.

### Story 8.6: Improve Task and Dashboard UX Navigation and Empty States

As a user,
I want clearer routing and empty-state guidance,
So that key actions are obvious across dashboard and task views.

**Acceptance Criteria:**

**Given** All Tasks filter is selected and no active tasks exist
**When** list view renders
**Then** create-task empty state is shown using the Active Tasks pattern.

**Given** primary app surfaces
**When** navigation renders
**Then** header tabs expose dashboard/tasks/momentum/leaderboard/profile routes
**And** active route highlighting and deep links remain correct.

**Given** task create/edit description input
**When** user resizes textarea
**Then** resize is limited to vertical direction and layout remains stable.

### Story 8.7: Expand Profile and Preferences with Secure Email Change

As a user,
I want clearer participation controls and secure email change,
So that privacy and account identity settings are easier to manage.

**Acceptance Criteria:**

**Given** profile preferences page
**When** leaderboard participation control renders
**Then** control is visually clear and accessible
**And** saved setting updates participation behavior consistently.

**Given** authenticated user requests email change
**When** current password and new email are submitted
**Then** verification flow is initiated and email is updated only after confirmation.

### Story 8.8: Deliver Public Profile Experience with Anonymous Participation Guardrails

As a user,
I want profile pages that respect visibility settings,
So that public stats are available only for opted-in participants.

**Acceptance Criteria:**

**Given** a public participant profile is requested
**When** page loads
**Then** profile shows approved statistics and momentum highlights.

**Given** an anonymous participant profile is requested
**When** page loads
**Then** statistics are not displayed
**And** UI shows the anonymous-participant message.

### Story 8.9: Harden Transactional Email Deliverability and Recovery Flows

As a platform operator,
I want reliable recovery and notification delivery,
So that users consistently receive security-critical and reminder emails.

**Acceptance Criteria:**

**Given** transactional email pipeline executes
**When** provider accepts or rejects messages
**Then** delivery status and provider identifiers are logged for diagnostics
**And** failed sends follow bounded retry policy.

**Given** recovery flow is triggered
**When** email cannot be delivered
**Then** user receives actionable guidance and support-safe error messaging
**And** operational alerts surface persistent failure patterns.

## Final Validation Summary

- All FRs (FR1-FR48) are mapped to at least one epic and covered by stories.
- Architecture starter-template requirement is satisfied by Story 1.1.
- Story sequencing avoids forward dependencies inside each epic.
- UX design requirements are covered across Stories 2.6, 3.4, 3.5, 4.5, and 5.3.
- Internal-role restrictions and auditability are covered in Epics 1 and 6.
- Integration parity requirements are covered in Epic 7.
- Expansion scope from 2026-05-03 briefing is decomposed into Epic 8 Stories 8.1 through 8.9.
