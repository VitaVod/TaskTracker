---
project_name: 'bmad'
user_name: 'Vitalii'
date: '2026-05-04'
sections_completed:
  - technology_stack
  - language_specific_rules
  - framework_specific_rules
  - testing_rules
  - code_quality_and_style_rules
  - development_workflow_rules
  - critical_dont_miss_rules
existing_patterns_found: 12
status: 'complete'
rule_count: 47
optimized_for_llm: true
---

# Project Context for AI Agents

_This file contains critical rules and patterns that AI agents must follow when implementing code in this project. Focus on unobvious details that agents might otherwise miss._

---

## Technology Stack & Versions

- Backend: ASP.NET Core Web API on .NET 9.0
- Backend packages: Microsoft.AspNetCore.Authentication.JwtBearer 9.0.6, Microsoft.AspNetCore.OpenApi 9.0.6
- Data access: EF Core 9.0.6 with Microsoft.EntityFrameworkCore.SqlServer 9.0.6
- Database: SQL Server (localdb in development; SQL Server or Azure SQL target)
- Frontend: Angular 20.3.x, TypeScript 5.9.x, RxJS 7.8.x, Zone.js 0.15.x
- Frontend build/test: Angular CLI 20.3.x, Jasmine 5.9, Karma 6.4
- API caching: Redis via Microsoft.Extensions.Caching.StackExchangeRedis 9.0.6 with in-memory fallback
- Test stack: xUnit 2.9.2, ASP.NET integration testing via Microsoft.AspNetCore.Mvc.Testing 9.0.6, EFCore.InMemory 9.0.6
- Language/compiler strictness:
  - TypeScript strict true, noImplicitReturns true, noFallthroughCasesInSwitch true, strictTemplates true
  - C# nullable enabled and implicit usings enabled

## Critical Implementation Rules

### Language-Specific Rules

- Keep TypeScript strict-safe at all times:
  - Do not bypass strict mode with any or non-null assertions unless there is no safe alternative.
  - Preserve strict Angular template compatibility for bindings and inputs.
- Keep API route and payload typing explicit:
  - Use strongly typed request and response contracts in Angular services and API controllers.
  - Keep route versioning and contract shape consistent with api/v1 conventions.
- Normalize user input at API boundaries:
  - Trim and normalize casing for enum-like fields before persistence.
  - Return validation failures as Problem Details payloads with stable code and traceId.
- Preserve deterministic time handling:
  - Use UTC for stored timestamps and server-side comparisons.
  - Do not mix local-time arithmetic into persistence or progression logic.
- Keep idempotency behavior explicit in write paths:
  - Require idempotency keys for completion-like mutation endpoints.
  - Treat replay detection as first-class response behavior, not log-only behavior.
- Follow C# expression-tree-safe EF conversions:
  - In HasConversion lambdas, avoid switch expressions that fail in expression tree translation.
  - Use conditional expressions or dedicated converter methods.
- Use async end-to-end with cancellation:
  - Pass CancellationToken through repository and controller call chains.
  - Avoid sync-over-async or blocking calls in request handling.

### Framework-Specific Rules

- Angular app structure:
  - Keep feature code under feature folders and shared cross-cutting code under shared folders.
  - Use standalone component patterns already present in app setup.
- Angular routing and authorization:
  - Protect authenticated routes with auth guards and role-sensitive routes with admin/support guards.
  - Keep unknown-route handling aligned with existing fallback redirect behavior.
- Angular HTTP pipeline:
  - Keep auth token attachment and 401 refresh handling inside interceptors, not duplicated in components.
  - Preserve single in-flight refresh coordination to avoid duplicate refresh requests.
- Angular service boundaries:
  - Keep API calls in shared services and keep components focused on UI state orchestration.
  - Reuse existing model contracts in shared models for service/component integration.
- ASP.NET Core dependency wiring:
  - Register repositories, validators, and domain services through DI in Program configuration.
  - Preserve authorization policies and role constants as centralized definitions.
- ASP.NET Core API conventions:
  - Keep controller routes under api/v1 and include ProducesResponseType metadata for contract clarity.
  - Return RFC7807-style Problem Details for auth, validation, forbidden, and not-found outcomes.
- Persistence and reliability:
  - Keep EF Core SQL Server provider with retry-on-failure configuration enabled.
  - Maintain idempotency records and deterministic progression updates for completion flows.
- Caching and read models:
  - Keep shared-view cache coordination centralized and avoid ad-hoc key construction in controllers.
  - Use configured TTL and freshness windows rather than hardcoded cache durations.

### Testing Rules

- Keep test types clearly separated:
  - Use backend integration tests for controller contracts, auth behavior, ownership boundaries, and persistence effects.
  - Use backend unit tests for pure logic such as token services, validators, and streak rule evaluation.
- Follow current naming and placement conventions:
  - Backend tests: tests/TaskTracker.Api.Tests/Integration/*ControllerTests.cs and Unit/*Tests.cs.
  - Frontend tests: colocated *.spec.ts files next to components, services, and guards.
- Validate contract-stable error behavior:
  - Assert Problem Details fields (type, title, status, code, traceId) for failure scenarios.
  - Include negative tests for malformed payloads, unauthorized requests, forbidden access, and not-found behavior.
- Cover ownership and impersonation boundaries:
  - Verify callers cannot mutate or read another user's resources even when spoofed identifiers are sent.
  - Validate role-restricted routes for admin and support access paths.
- Test idempotency and deterministic progression:
  - Verify completion replay behavior does not duplicate XP or celebratory side effects.
  - Assert streak and progression outputs are deterministic for identical event streams and timezone context.
- Keep frontend tests behavior-focused:
  - Mock services and verify user-visible state changes, navigation, and guard or interceptor reactions.
  - Prefer assertions on rendered outcomes and state transitions over implementation details.
- Preserve fixture isolation:
  - Avoid shared mutable test state across test cases.
  - Seed only minimum required data per test and assert persisted outcomes explicitly.
- Ensure regression coverage for cross-cutting concerns:
  - Add tests when touching auth token lifecycle, cache invalidation, validation contracts, and critical domain rules.

### Code Quality & Style Rules

- Respect existing formatting and tooling baselines:
  - Frontend uses 2-space indentation, single quotes for TypeScript, and configured Prettier defaults.
  - Keep markdown trailing-whitespace behavior aligned with editorconfig settings.
- Preserve strictness and type safety:
  - Do not relax TypeScript strict compiler settings to satisfy new code.
  - Keep nullable-safe C# code paths and avoid suppressing nullability warnings without a clear reason.
- Keep naming consistent with current codebase:
  - Angular files use kebab-case filenames with feature-focused suffixes (component, service, guard, interceptor, models).
  - C# types and members use PascalCase; local variables and parameters use camelCase.
- Maintain project organization boundaries:
  - Keep frontend domain features in feature folders and cross-feature concerns in shared.
  - Keep backend layering clear: Controllers, Repositories or Services, Infrastructure or Persistence.
- Keep contracts explicit and stable:
  - Prefer shared contract records and types for request and response shapes over ad-hoc anonymous structures.
  - Keep API route, payload, and error-code conventions stable when extending endpoints.
- Keep comments purposeful and minimal:
  - Add comments only for non-obvious intent, invariants, and edge-case handling.
  - Avoid redundant comments that restate obvious code behavior.
- Avoid hidden behavioral changes in refactors:
  - Do not alter authorization, idempotency, cache windows, or validation semantics during style-only edits.
  - Keep logging and trace-correlation behavior intact across modifications.
- Prefer focused, minimal diffs:
  - Limit changes to files and lines relevant to the task to reduce regression risk.
  - Avoid unrelated reformatting and broad renames unless explicitly requested.

### Development Workflow Rules

- Keep implementation aligned to planning artifacts:
  - Treat _bmad-output/planning-artifacts as the source for architecture, PRD, and UX constraints.
  - Validate behavior changes against implementation-artifact acceptance intent before merging.
- Preserve migration and schema discipline:
  - For backend model changes, create explicit EF Core migrations and keep snapshots consistent.
  - Do not mix unrelated schema changes into feature-focused migrations.
- Keep environment configuration explicit:
  - Use configuration sections for JWT, cache, progression, and connection settings; avoid hardcoded runtime values.
  - Keep production-sensitive values externalized and never commit real secrets.
- Maintain API compatibility in iterative changes:
  - Keep existing route and version patterns and error-contract stability for frontend compatibility.
  - Coordinate frontend model updates with backend contract changes in the same change set when required.
- Protect release safety with verification steps:
  - Run relevant backend and frontend tests for touched areas before finalizing changes.
  - Add or update tests with functional changes, especially for auth, ownership, idempotency, and progression logic.
- Keep operational observability intact:
  - Preserve trace correlation and structured logging on critical mutation flows.
  - Keep privileged-action audit paths intact when touching admin, support, or policy-restricted behavior.
- Follow small-batch change practices:
  - Prefer narrowly scoped commits tied to one story or intent.
  - Avoid broad refactors during feature delivery unless explicitly requested.
- Respect existing in-flight work:
  - Do not revert unrelated working-tree changes.
  - Minimize overlap with staged edits in adjacent files unless the task explicitly requires it.

### Critical Don't-Miss Rules

- Do not break deterministic progression semantics:
  - Never grant XP twice for the same completion event.
  - Preserve idempotency-key checks and replay-safe responses for completion toggles.
- Do not weaken ownership and authorization boundaries:
  - Never trust caller-supplied user identifiers for resource ownership.
  - Always derive acting user identity from authenticated claims and enforce policy checks server-side.
- Do not change error-contract shape casually:
  - Keep Problem Details type, status, code, and traceId present and stable for clients and tests.
  - Avoid introducing ad-hoc error payload formats for new endpoints.
- Do not mix local time into core persistence logic:
  - Store and compare canonical times in UTC.
  - Apply timezone projection only where business rules explicitly require local-day semantics.
- Do not bypass secure session lifecycle:
  - Keep token-type validation and revoked-session checks in place for protected endpoints.
  - Preserve safe logout behavior that remains idempotent when a session is already revoked.
- Do not undermine cache correctness:
  - Avoid direct cache-key writes from controllers that bypass shared cache coordination.
  - Keep invalidation tied to authoritative domain mutations and configured freshness windows.
- Do not introduce EF conversion pitfalls:
  - Avoid switch expressions in expression-tree-based EF HasConversion lambdas.
  - Prefer conditional expressions or converter methods that translate reliably.
- Do not merge behavior changes without regression tests:
  - Any change touching auth, ownership, idempotency, streak logic, or contracts must include test updates.
  - Keep negative-path and replay or edge-case assertions where applicable.

---

## Usage Guidelines

**For AI Agents:**

- Read this file before implementing any code.
- Follow all rules in this file exactly as documented.
- When in doubt, prefer the more restrictive option.
- Update this file when new non-obvious patterns emerge.

**For Humans:**

- Keep this file lean and focused on agent-critical guidance.
- Update when technology versions or architecture constraints change.
- Review quarterly for outdated or redundant rules.
- Remove rules that become obvious and no longer add implementation value.

Last Updated: 2026-05-04
