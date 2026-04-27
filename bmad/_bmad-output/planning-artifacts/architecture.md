---
stepsCompleted:
  - 1
  - 2
  - 3
  - 4
  - 5
  - 6
  - 7
  - 8
status: 'complete'
completedAt: '2026-04-24'
lastStep: 8
inputDocuments:
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/ux-design-specification.md
  - _bmad-output/planning-artifacts/product-brief-bmad.md
  - _bmad-output/planning-artifacts/product-brief-bmad-distillate.md
workflowType: 'architecture'
project_name: 'bmad'
user_name: 'Vitalii'
date: '2026-04-24'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

Functional Requirements:
The product requires end-to-end support for authenticated task management, immediate progress reinforcement, social comparison surfaces, and operational governance. Architecturally, this implies clear bounded modules for identity and access, task lifecycle management, progression engine (XP/streak), leaderboard/statistics read models, notifications, integrations, and internal operations tooling (admin/support). The requirement spread indicates a single-user core domain with platform-level shared views and role-gated internal controls.

Non-Functional Requirements:
The architecture is primarily shaped by deterministic correctness and trust guarantees:
- Deterministic and idempotent XP/streak processing under retries and duplicates
- Strong server-side authorization with strict ownership boundaries
- Secure token/session lifecycle and auditable privileged actions
- Responsive read paths for leaderboard/statistics via caching/optimization
- WCAG 2.1 AA accessibility for all critical flows
- Reliability and traceability sufficient for support dispute resolution

Scale and Complexity:
- Primary domain: gamified productivity web platform
- Complexity level: medium
- Estimated architectural components: 10-14 major components/services (or modular domains in a modular monolith), including identity, tasks, progression engine, ranking/statistics, notifications, integrations, moderation/support, analytics/events, and cross-cutting platform capabilities.

### Technical Constraints and Dependencies

- Platform direction is fixed for MVP: ASP.NET backend with Angular frontend.
- Authenticated SPA for product experience; public pages have separate SEO requirements.
- Real-time or near-real-time feedback required for completion, XP/streak confirmation, and fresh ranking/statistics views.
- Time semantics are first-class: explicit timezone source of truth and day-boundary policy are mandatory.
- Ranking integrity requires deterministic tie-break rules and anti-gaming controls.
- Integration pathways must obey the same validation/authorization rules as first-party flows.

### Cross-Cutting Concerns Identified

- Identity, authorization, and role-based capability boundaries
- Idempotent command handling and deduplication strategy
- Event/audit trail design for explainability and support operations
- Timezone/day-boundary policy consistency across API and UI
- Cache strategy and read-model freshness for leaderboards/statistics
- Observability for business funnels and integrity monitoring
- Accessibility compliance and responsive interaction quality
- Privacy-safe public identity exposure in competitive surfaces

## Starter Template Evaluation

### Primary Technology Domain

Full-stack web application, with Angular SPA frontend and ASP.NET Core backend API, based on project requirements and existing product artifacts.

### Starter Options Considered

Option 1: Separate Angular + ASP.NET Core projects (recommended)
- Angular created with ng new
- Backend created with dotnet new webapi
- Clear frontend/backend boundaries
- Strong fit for role-based APIs, idempotent domain logic, and scalable read models

Option 2: Single-template SPA-hosted backend approach
- Faster initial setup for demos
- Weaker separation and less flexibility for independent scaling and deployment
- Not ideal for this project's operational/admin/support and cross-cutting concerns

Option 3: Monorepo orchestration framework plus .NET
- Can unify workspace tooling
- Adds setup complexity early
- Better deferred until post-MVP if team needs large-scale workspace management

### Selected Starter: Angular CLI + ASP.NET Core Web API (separate projects)

Rationale for Selection:
- Best alignment with your fixed tech direction (Angular + ASP.NET)
- Explicit data platform preference: SQL Server over PostgreSQL
- Supports strict domain boundaries and deterministic backend processing
- Keeps architecture clear for AI agent consistency and later epic/story implementation
- Scales cleanly for independent frontend and API deployment

Initialization Commands:

Frontend:
npm install -g @angular/cli
ng new task-tracker-web --routing --style scss --package-manager npm --strict

Backend:
dotnet new webapi -n TaskTracker.Api -o task-tracker-api --framework net9.0
dotnet add task-tracker-api/TaskTracker.Api.csproj package Microsoft.EntityFrameworkCore.SqlServer
dotnet add task-tracker-api/TaskTracker.Api.csproj package Microsoft.EntityFrameworkCore.Design

Optional workspace scaffold:
dotnet new sln -n TaskTracker
dotnet sln TaskTracker.sln add task-tracker-api/TaskTracker.Api.csproj

### Architectural Decisions Provided by Starter

Language and Runtime:
- Frontend: TypeScript with Angular 21 CLI conventions
- Backend: C# on ASP.NET Core .NET 9 Web API template

Styling Solution:
- SCSS component styling in Angular with design-token friendly path for your UX system

Build Tooling:
- Angular CLI standard build/serve pipeline
- dotnet CLI build/run/test pipeline

Testing Framework:
- Angular default modern unit-testing setup from current CLI defaults
- Backend test projects can be added in first implementation stories

Code Organization:
- Explicit split of UI concerns and domain/API concerns
- Easier enforcement of ownership, authorization, and observability boundaries

Development Experience:
- Mature CLI tooling on both sides
- Predictable scaffolding and project structure
- Good fit for CI and artifact-level traceability

### Data Platform Preference (Captured)

- Primary relational database: Microsoft SQL Server
- EF provider: Microsoft.EntityFrameworkCore.SqlServer (aligned with .NET 9 / EF Core 9)
- Supported deployment targets: SQL Server 2022+ and Azure SQL Database
- Migration strategy: EF Core migrations generated in API project and applied via deployment pipeline
- Connection resiliency baseline: enable SQL retry policy in DbContext configuration

Note: Project initialization using these commands should be the first implementation story.

## Core Architectural Decisions

### Decision Priority Analysis

Critical Decisions (Block Implementation):
- Architecture style: modular monolith backend with clear domain modules and internal service boundaries
- Data platform: SQL Server with EF Core SQL Server provider
- API style: RESTful JSON API with versioned routes under /api/v1
- Authentication and authorization: JWT bearer tokens with role-based authorization for user, admin, support
- Deterministic progression updates: idempotent completion processing using completion event deduplication keys

Important Decisions (Shape Architecture):
- Read performance strategy: cache-first leaderboard and global statistics read models with explicit invalidation
- Time semantics policy: UTC event storage plus user timezone projection for streak boundary calculations
- Error contract: standardized Problem Details style envelope with machine-readable code and trace correlation id
- Observability baseline: structured logging, audit log for privileged actions, domain event traceability
- Deployment model: separate frontend and backend deployment units, shared environment configuration policy

Deferred Decisions (Post-MVP):
- Multi-region active-active database strategy
- Event bus extraction from in-process domain events to external broker
- Advanced query optimization features beyond core index and cache strategy

### Data Architecture

- Database engine: Microsoft SQL Server (primary), SQL Server 2022+ or Azure SQL Database
- ORM and data access: EF Core 9 with Microsoft.EntityFrameworkCore.SqlServer
- Data modeling approach: normalized relational model for task, completion, xp_ledger, streak_snapshot, leaderboard_snapshot, and audit_log domains
- Validation approach: request DTO validation at API boundary plus domain invariant checks in application services
- Migration approach: EF Core migrations generated in API project and applied during deployment pipeline (not at app startup)
- Caching strategy: distributed cache for leaderboard/statistics reads with short TTL and event-driven invalidation on completion commits

### Authentication and Security

- Auth method: JWT access tokens; refresh token flow for session continuity
- Authorization: policy-based role checks in ASP.NET Core for user/admin/support capabilities
- API security baseline: authenticated endpoints for all task/progress data, server-side ownership checks on every protected resource
- Encryption: TLS in transit and SQL Server encryption-at-rest features enabled by environment policy
- Auditability: immutable audit records for admin/support actions including actor, target, reason, timestamp, correlation id

### API and Communication Patterns

- API pattern: REST over HTTPS, resource-oriented endpoints, pagination for list and leaderboard views
- API docs: OpenAPI generated from ASP.NET Core with documented response/error schemas
- Error handling: RFC 7807-compatible Problem Details payload with stable application error codes
- Rate limiting: scoped policies by endpoint category (auth, write-heavy, public read-heavy)
- Internal communication: in-process domain events in modular monolith; idempotent command handlers for mutation flows

### Frontend Architecture

- App style: Angular SPA with route-based feature organization
- State approach: feature-scoped service/store pattern; server state as source of truth for progress and leaderboard data
- Component architecture: Angular Material base components plus product-specific wrappers for streak/xp/leaderboard widgets
- Performance strategy: route-level lazy loading, selective prefetch, API response caching for read-heavy screens
- Resilience UX: optimistic UI only where safe; authoritative reconciliation from API for progression-critical actions

### Infrastructure and Deployment

- Hosting strategy: independent frontend and backend deployables with environment-specific configuration
- CI/CD baseline: build, lint, test, migration validation, and deployment gates per project
- Environment config: strict separation of local/dev/stage/prod configs; secrets via secure secret store
- Monitoring: centralized logs, request traces, and key product metrics (activation, completion latency, streak correctness)
- Scaling baseline: stateless API instances horizontally scaled; SQL read and cache tuning for leaderboard/stat pages

### Decision Impact Analysis

Implementation Sequence:
1. Scaffold Angular and ASP.NET projects
2. Establish auth and authorization foundation
3. Implement SQL Server data model and migration pipeline
4. Implement task and completion command flows with idempotency
5. Implement progression read models, caching, and leaderboard/statistics endpoints
6. Implement frontend features tied to deterministic backend contracts
7. Add observability, audit, and operational tooling hardening

Cross-Component Dependencies:
- Completion flow depends on idempotent command handling and SQL transaction boundaries
- Streak and XP accuracy depends on timezone policy and event ordering guarantees
- Leaderboard freshness depends on cache invalidation tied to committed completion events
- Support/admin troubleshooting depends on structured audit and correlation-aware logging

## Implementation Patterns and Consistency Rules

### Pattern Categories Defined

Critical conflict points identified:
- Naming conventions across database, API, and code
- API and error payload formats
- Project organization and test placement
- State update and event naming patterns
- Error, retry, and loading behavior

### Naming Patterns

Database naming conventions:
- Tables: plural snake_case (users, tasks, completion_events, xp_ledger, streak_snapshots)
- Columns: snake_case
- Primary keys: id
- Foreign keys: singular_ref_id (user_id, task_id)
- Indexes: ix_table_columns (ix_tasks_user_id_due_at)
- Unique indexes: ux_table_columns (ux_completion_events_idempotency_key)

API naming conventions:
- Base path: /api/v1
- Resources: plural kebab-case paths (leaderboard-streak)
- Query params: camelCase in HTTP layer (pageSize, sortBy)
- Route params: braces in spec, colon in Angular router usage
- Headers: standard headers first; custom headers prefixed with X-TaskTracker-

Code naming conventions:
- C# types: PascalCase
- C# members/locals: camelCase
- Angular classes/components/services: PascalCase class names
- TypeScript variables/functions: camelCase
- File names:
  - Angular feature files: kebab-case
  - C# files: PascalCase matching primary type

### Structure Patterns

Project organization:
- Backend modular monolith by feature:
  - TaskTracker.Api
  - TaskTracker.Application
  - TaskTracker.Domain
  - TaskTracker.Infrastructure
- Frontend by feature area under src/app/features
- Shared frontend concerns under src/app/shared
- Backend tests in dedicated test projects, frontend tests co-located with components/services

File structure patterns:
- Environment configs:
  - Angular: standard environment files
  - ASP.NET: appsettings by environment
- Migration files only in infrastructure/data project area
- API contracts grouped by feature
- No cross-feature imports bypassing designated shared modules

### Format Patterns

API response formats:
- Success reads: object or collection directly under stable contract per endpoint
- Mutations: return resource snapshot or operation result object with deterministic fields
- Errors: RFC 7807 Problem Details with:
  - type
  - title
  - status
  - code
  - traceId
  - errors (for validation)

Data exchange formats:
- JSON fields: camelCase at API boundary
- Server domain/persistence may use C# conventions internally
- Dates and times: ISO 8601 UTC in API payloads
- IDs: GUID strings in API contracts
- Booleans: true/false only
- Null handling: explicit null when known-empty, omit only when truly optional and documented

### Communication Patterns

Event system patterns:
- Domain event names: PascalCase with past-tense intent (TaskCompleted, XpAwarded, StreakEvaluated)
- Integration/event payload shape:
  - eventId
  - eventType
  - occurredAtUtc
  - actorUserId
  - aggregateId
  - version
  - payload
- Idempotency key required for completion and reward-triggering commands

State management patterns:
- Frontend server-state is authoritative for XP/streak/leaderboard
- Optimistic UI allowed for task toggle only with reconciliation
- Feature-scoped state stores/services, no global mutable singleton for domain state
- Action/command naming: verbNoun (completeTask, refreshLeaderboard)

### Process Patterns

Error handling patterns:
- Map domain and validation failures to stable error codes
- Never expose raw exception details to clients
- Correlate logs and API errors through traceId
- Retry only transient infrastructure failures, never business-rule failures

Loading state patterns:
- Per-feature loading flags plus per-operation loading flags for mutation actions
- Skeleton/loading placeholders on read-heavy pages
- Disable repeated submit actions while in-flight for idempotency-sensitive operations
- Show deterministic completion result messages from server-confirmed outcome

### Enforcement Guidelines

All AI agents must:
- Follow naming conventions exactly as defined above
- Use shared API/error contract shapes without ad-hoc variants
- Respect module boundaries and project structure rules
- Preserve timezone and idempotency rules in all progression-related code
- Add or update tests in the defined project/test locations

Pattern enforcement:
- Pull request checklist includes naming, contract, and boundary checks
- Contract changes require architecture doc update first
- Linting and static analysis enforce baseline style and file conventions
- CI validates tests, formatting, and migration consistency

### Pattern Examples

Good examples:
- completion_events.idempotency_key with unique index ux_completion_events_idempotency_key
- API error with status plus code plus traceId
- Angular feature component under features/tasks and shared widget under shared/ui

Anti-patterns:
- Mixing snake_case and camelCase for the same API field set
- Returning ad-hoc error objects that bypass Problem Details
- Writing XP/streak logic in frontend without server confirmation
- Creating cross-feature imports that bypass shared abstractions

## Project Structure and Boundaries

### Complete Project Directory Structure

TaskTracker/
- README.md
- .gitignore
- TaskTracker.sln
- .editorconfig
- .github/
  - workflows/
    - ci.yml
    - release.yml
- docs/
  - architecture-decisions/
  - api-contracts/
  - operational-runbooks/
- deploy/
  - docker/
    - api.Dockerfile
    - web.Dockerfile
  - k8s/
    - api/
    - web/
    - shared/
  - scripts/
    - apply-migrations.ps1
    - seed-reference-data.ps1
- task-tracker-api/
  - TaskTracker.Api/
    - TaskTracker.Api.csproj
    - Program.cs
    - appsettings.json
    - appsettings.Development.json
    - appsettings.Staging.json
    - appsettings.Production.json
    - Controllers/
      - AuthController.cs
      - TasksController.cs
      - ProgressController.cs
      - LeaderboardsController.cs
      - StatsController.cs
      - AdminController.cs
      - SupportController.cs
    - Contracts/
      - Requests/
      - Responses/
      - Errors/
    - Middleware/
      - ExceptionMappingMiddleware.cs
      - CorrelationIdMiddleware.cs
    - Authorization/
      - Policies/
      - Requirements/
    - Health/
      - HealthChecksRegistration.cs
    - OpenApi/
      - SwaggerConfiguration.cs
  - TaskTracker.Application/
    - TaskTracker.Application.csproj
    - Abstractions/
    - Features/
      - Auth/
      - Tasks/
      - Completion/
      - Progression/
      - Leaderboards/
      - Stats/
      - Admin/
      - Support/
    - Behaviors/
      - ValidationBehavior.cs
      - IdempotencyBehavior.cs
    - Events/
      - DomainEvents/
      - IntegrationEvents/
  - TaskTracker.Domain/
    - TaskTracker.Domain.csproj
    - Entities/
    - ValueObjects/
    - DomainServices/
    - Rules/
    - Events/
    - Repositories/
  - TaskTracker.Infrastructure/
    - TaskTracker.Infrastructure.csproj
    - Persistence/
      - DbContexts/
        - TaskTrackerDbContext.cs
      - Configurations/
      - Migrations/
      - Repositories/
    - Outbox/
    - Caching/
    - Messaging/
    - Security/
      - Token/
    - Time/
      - UtcClock.cs
      - UserTimeZoneProjector.cs
    - Telemetry/
    - Audit/
  - TaskTracker.Api.Tests/
    - Unit/
    - Integration/
    - Contract/
    - TestUtilities/
    - Fixtures/
- task-tracker-web/
  - package.json
  - angular.json
  - tsconfig.json
  - eslint.config.js
  - src/
    - main.ts
    - styles.scss
    - app/
      - app.config.ts
      - app.routes.ts
      - core/
        - api/
        - auth/
        - interceptors/
        - guards/
        - error/
        - telemetry/
      - shared/
        - ui/
        - utils/
        - models/
        - constants/
      - features/
        - auth/
          - pages/
          - components/
          - services/
          - state/
        - tasks/
          - pages/
          - components/
          - services/
          - state/
        - progression/
          - pages/
          - components/
          - services/
          - state/
        - leaderboards/
          - pages/
          - components/
          - services/
          - state/
        - stats/
          - pages/
          - components/
          - services/
          - state/
        - admin/
          - pages/
          - components/
          - services/
          - state/
        - support/
          - pages/
          - components/
          - services/
          - state/
    - environments/
      - environment.ts
      - environment.development.ts
      - environment.staging.ts
      - environment.production.ts
  - tests/
    - e2e/
    - contract/
    - fixtures/

### Architectural Boundaries

API boundaries:
- External API surface only through TaskTracker.Api controllers under /api/v1
- No direct infrastructure access from controllers
- Controller to Application layer only through commands/queries and contracts
- Authentication and ownership checks enforced in API boundary

Component boundaries:
- Angular feature modules interact through core api services and typed contracts
- Shared UI in app/shared/ui only, no feature-to-feature component imports
- Feature state remains inside each feature folder

Service boundaries:
- Domain rules in TaskTracker.Domain only
- Use case orchestration in TaskTracker.Application only
- Data and external concerns in TaskTracker.Infrastructure only
- Idempotency, validation, and audit behaviors centralized in Application pipeline

Data boundaries:
- SQL Server schema managed only by Infrastructure migrations
- Repositories implemented in Infrastructure, abstractions in Domain/Application
- Cache updates and invalidation happen after successful transactional commits
- Read models for leaderboard and stats isolated from write path entities

### Requirements to Structure Mapping

FR groups to folders:
- Accounts and identity: TaskTracker.Application/Features/Auth and task-tracker-web/src/app/features/auth
- Task CRUD: TaskTracker.Application/Features/Tasks and task-tracker-web/src/app/features/tasks
- XP and streak engine: TaskTracker.Application/Features/Completion and Progression plus Domain rules
- Leaderboards and global stats: TaskTracker.Application/Features/Leaderboards and Stats plus web feature folders
- Admin and support operations: TaskTracker.Application/Features/Admin and Support plus corresponding web features
- Notifications and reminders: TaskTracker.Application/Features/Notifications with Infrastructure delivery adapters
- Integrations: TaskTracker.Application/Features/Integrations plus Infrastructure gateway clients

Cross-cutting concerns:
- Authorization policies: TaskTracker.Api/Authorization
- Error format and correlation: TaskTracker.Api/Middleware and TaskTracker.Api/Contracts/Errors
- Audit trail: TaskTracker.Infrastructure/Audit
- Timezone policy: TaskTracker.Infrastructure/Time and Domain value objects
- Observability: TaskTracker.Infrastructure/Telemetry and task-tracker-web/src/app/core/telemetry

### Integration Points

Internal communication:
- API to Application via request handlers
- Application to Domain via aggregates, services, and rules
- Application to Infrastructure via ports/adapters and repository contracts
- Frontend to backend via typed API client services in app/core/api

External integrations:
- Email provider through Infrastructure notification adapters
- Optional third-party task import through Infrastructure integration clients
- SQL Server and distributed cache as platform integrations

Data flow:
- Command path: web action to API to Application command to Domain rules to SQL commit to cache invalidation to response
- Query path: web request to API query handler to cache/read model to SQL fallback to response
- Support path: API to audit/event traces to diagnostic response payloads

### File Organization Patterns

Configuration files:
- Environment-specific appsettings and Angular environments separated by deployment stage
- Secrets excluded from repository and injected by runtime environment

Source organization:
- Backend organized by clean boundaries Api/Application/Domain/Infrastructure
- Frontend organized by feature-first layout with strict shared/core boundaries

Test organization:
- Backend unit/integration/contract test projects separated by purpose
- Frontend component/service tests co-located, e2e tests under tests/e2e

Asset organization:
- Frontend static assets under task-tracker-web/src/assets
- Deployment assets and scripts under deploy

### Development Workflow Integration

Development server structure:
- Backend and frontend run independently with environment-specific config
- Local SQL Server or Azure SQL dev instance via connection string profiles

Build process structure:
- CI builds backend projects and frontend workspace separately
- Migration validation step runs before release deployment

Deployment structure:
- Migration apply script executed before API rollout
- Web deployment references versioned API base URL and environment config

## Architecture Validation Results

### Coherence Validation

Decision compatibility:
- Technology choices are coherent: Angular 21, ASP.NET Core .NET 9, EF Core 9, SQL Server 2022+.
- No version-level contradictions were identified in the selected stack.
- API, auth, data, and deployment choices are aligned with modular monolith boundaries.

Pattern consistency:
- Naming patterns are consistent across database, API, and code.
- Error and response formatting rules align with API conventions.
- Idempotency, timezone, and audit patterns reinforce deterministic progression behavior.

Structure alignment:
- Directory layout supports the chosen boundaries: Api, Application, Domain, Infrastructure, feature-first Angular frontend.
- Integration points and ownership boundaries are explicit and enforceable.
- Test and deployment structure aligns with CI/CD and migration strategy.

### Requirements Coverage Validation

Feature and FR coverage:
- Identity, task CRUD, progression, leaderboards, stats, admin/support, notifications, and integrations all map to concrete architecture areas.
- Cross-cutting requirements like ownership enforcement and support traceability are structurally covered.

Non-functional coverage:
- Performance: caching strategy and read model separation for leaderboard/stats.
- Security: policy-based authz, server-side ownership checks, audit trail, TLS and encryption-at-rest baseline.
- Reliability: idempotency and event ordering controls for completion processing.
- Accessibility and UX responsiveness are supported through frontend structure and standards carried from UX and PRD outputs.

### Implementation Readiness Validation

Decision completeness:
- Critical decisions are documented and actionable.
- SQL Server preference is captured and reflected in stack and commands.

Structure completeness:
- Project tree is explicit and implementation-ready.
- Integration points, test placement, and environment structure are defined.

Pattern completeness:
- Conflict-prone areas have concrete consistency rules.
- Enforcement guidance is defined for CI and PR review behavior.

### Gap Analysis Results

Critical gaps:
- None identified.

Important gaps:
- Add explicit Notifications and Integrations module folders in backend structure section for perfect FR-to-folder parity.
- Add frontend contract-test placement notes tied to API versioning strategy.

Nice-to-have gaps:
- Add sample migration naming policy and rollback conventions.
- Add API deprecation policy for future version evolution.

### Architecture Completeness Checklist

Requirements analysis:
- Completed.

Architectural decisions:
- Completed.

Implementation patterns:
- Completed.

Project structure:
- Completed.

### Architecture Readiness Assessment

Overall status:
- Ready for implementation.

Confidence level:
- High.

Key strengths:
- Deterministic progression architecture with idempotency.
- Strong modular boundaries and clear AI-agent consistency rules.
- SQL Server aligned across data, tooling, and deployment.

Areas for future enhancement:
- Expand integration strategy details.
- Add deeper observability dashboards and SLO targets in a later pass.

### Implementation Handoff

AI agent guidelines:
- Follow architecture decisions and consistency rules in architecture.md.
- Respect module boundaries and API contract patterns.
- Preserve idempotency, timezone, and audit constraints.

First implementation priority:
1. Scaffold backend and frontend projects from selected starter commands.
2. Establish auth and SQL Server migration baseline.
3. Implement task and completion flow with idempotency.
