# Story 1.5: Implement Role Policies and Ownership Authorization Baseline

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a platform owner,
I want server-side role and ownership enforcement,
so that users and internal roles can access only permitted capabilities.

## Acceptance Criteria

1. Given any protected endpoint, when the request is evaluated, then role policy and ownership checks are applied server-side, and unauthorized and forbidden responses use consistent error contracts.
2. Given admin or support routes, when a standard user attempts access, then access is denied, and the denial is logged with trace context.

## Tasks / Subtasks

- [x] Establish explicit role and ownership authorization policy baseline in API (AC: 1, 2)
  - [x] Add role claims and policy registration in the API startup/auth pipeline for `User`, `Admin`, and `Support` capabilities.
  - [x] Define a consistent authorization approach (policy-based and/or endpoint attributes) and apply it to all currently protected account and auth-adjacent routes.
  - [x] Introduce reusable ownership authorization helper(s) for current-user resource access checks so future task/progression endpoints can adopt the same pattern.

- [x] Standardize unauthorized/forbidden Problem Details contracts (AC: 1)
  - [x] Ensure all protected endpoints return deterministic RFC 7807 payloads for both `401` and `403`, including stable `type`, `title`, `status`, `code`, and `traceId` extension fields.
  - [x] Add a shared helper/middleware approach to avoid per-controller response drift.
  - [x] Validate that ownership-denied and role-denied scenarios remain distinguishable via stable application error codes.

- [x] Enforce and validate admin/support route restrictions (AC: 2)
  - [x] Add at least one concrete admin-facing and one support-facing protected API route (or protect existing placeholders) to prove role gates are active.
  - [x] Confirm standard user principals are rejected for admin/support endpoints with `403 Forbidden` Problem Details.
  - [x] Keep support access read-only by default; do not introduce destructive mutations in this story.

- [x] Add trace-aware denial logging and audit baseline (AC: 2)
  - [x] Log every authorization denial with actor identity (if available), attempted capability/route, decision outcome, and request trace identifier.
  - [x] Keep logs structured and correlation-friendly for future support/admin troubleshooting.
  - [x] Ensure denial events avoid leaking secrets or sensitive token material.

- [x] Add backend and frontend tests for authz contracts and UX behavior (AC: 1, 2)
  - [x] Backend integration tests for unauthenticated (`401`), forbidden role (`403`), and ownership-denied (`403`) scenarios across protected routes.
  - [x] Backend integration/unit tests asserting consistent Problem Details shape and required extensions (`code`, `traceId`).
  - [x] Frontend tests for recoverable unauthorized/forbidden handling paths (route guard, API error handling, and user-facing messaging fallback) without session corruption.
  - [x] Re-run story 1.2 to 1.4 auth/session/account regression suite to ensure no behavior breaks in register/login/refresh/logout/profile flows.

## Dev Notes

- Use existing controllers and auth foundations as the baseline instead of introducing a new authorization stack. Current implementation touchpoints are in `TaskTracker.Api/Controllers/AuthController.cs` and `TaskTracker.Api/Controllers/AccountController.cs`.
- Keep API surface under `/api/v1` and preserve the project's RFC 7807 error model with stable application codes and trace correlation.
- Existing account endpoints already resolve current-user identity from token claims; extend this pattern for ownership checks and deny-by-default role boundaries.
- Story 1.3 established secure server-side session authority (refresh rotation/revocation). Story 1.5 must build on that trust boundary and not bypass token/session validation.
- SQL Server remains the required persistence target and should continue to back authorization-related entity changes or audit extensions.
- Favor centralized policy/authorization helpers to reduce duplicated checks and inconsistent status-code handling.

### API Contracts

**Forbidden Response Contract (role or ownership denied):**
```
HTTP/1.1 403 Forbidden
Content-Type: application/problem+json

{
  "type": "https://api.tasktracker.local/problems/forbidden",
  "title": "Forbidden",
  "status": 403,
  "code": "authz.access.denied",
  "traceId": "0HN1FDHJ..."
}
```

**Unauthorized Response Contract (missing/invalid auth):**
```
HTTP/1.1 401 Unauthorized
Content-Type: application/problem+json

{
  "type": "https://api.tasktracker.local/problems/authentication-failed",
  "title": "Authentication Failed",
  "status": 401,
  "code": "auth.session.invalid",
  "traceId": "0HN1FDHJ..."
}
```

### Previous Story Intelligence

- Story 1.4 already introduced account profile/settings ownership-safe mutation patterns and explicit Problem Details extension fields (`code`, `traceId`). Reuse these patterns as the canonical contract baseline.
- Story 1.3 made session revocation and refresh replay handling authoritative on the server. Authorization gates in this story must evaluate only validated caller identity from trusted token/session context.
- Keep current frontend route-guard and auth interceptor behavior stable; authorization-denied handling should be additive and not regress auth UX.

### Project Structure Notes

- Expected backend touch points:
  - `task-tracker-api/TaskTracker.Api/Program.cs`
  - `task-tracker-api/TaskTracker.Api/Controllers/AuthController.cs`
  - `task-tracker-api/TaskTracker.Api/Controllers/AccountController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Auth/`
  - `task-tracker-api/TaskTracker.Api/Features/Account/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/` (if role or audit persistence schema changes are needed)
  - `task-tracker-api/tests/TaskTracker.Api.Tests/`
- Expected frontend touch points:
  - `task-tracker-web/src/app/app.routes.ts`
  - `task-tracker-web/src/app/shared/guards/`
  - `task-tracker-web/src/app/shared/services/`
  - `task-tracker-web/src/app/features/account/`

### Testing Requirements

- Validate deterministic distinction between authentication failures (`401`) and authorization failures (`403`) for protected APIs.
- Validate that admin/support policies block standard users and allow authorized principals.
- Validate ownership boundaries on current-user resources to prevent cross-user access.
- Validate denial logging includes trace correlation and route/policy context.
- Validate frontend gracefully handles `401` and `403` without data leakage or broken navigation state.

### References

- Story definition and ACs: [Source: _bmad-output/planning-artifacts/epics.md, Epic 1, Story 1.5]
- Authorization model and risk mitigations: [Source: _bmad-output/planning-artifacts/prd.md, Authorization and Access Control; Risk Mitigations]
- API/security/error contract standards: [Source: _bmad-output/planning-artifacts/architecture.md, Authentication and Security; API and Communication Patterns; Format Patterns]
- UX error and accessibility behavior: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Feedback Patterns; Form Patterns; Accessibility Strategy]
- Existing implementation baseline: [Source: _bmad-output/implementation-artifacts/1-4-build-profile-and-account-settings-management.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Added role-based policy baseline with reusable ownership authorization requirement and handler.
- Centralized `401` and `403` RFC 7807 contracts with stable `code` and `traceId` fields via auth challenge handling and authorization middleware result handler.
- Added concrete admin and support read-only endpoints protected by role policies.
- Added denial logging with user, route, method, decision code, and trace correlation.
- Added role persistence (`User` default), role claim issuance in JWT access/refresh tokens, and role resolution during refresh-token rotation.
- Added integration and unit coverage for unauthorized/forbidden contracts, ownership-denied behavior, role restrictions, and denial logging.
- Added frontend interceptor test to ensure `403` does not clear tokens or force login redirect.
- Backend and frontend suites passed locally.

### File List

- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/TaskTracker.Api/Controllers/AccountController.cs
- task-tracker-api/TaskTracker.Api/Controllers/AuthController.cs
- task-tracker-api/TaskTracker.Api/Controllers/OperationsController.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Repositories/IAuthRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Repositories/AuthRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Tokens/IJwtTokenService.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Tokens/JwtTokenService.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Authorization/AppRoles.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Authorization/AppPolicies.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Authorization/OwnershipRequirement.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Authorization/RouteUserOwnershipHandler.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Authorization/TraceableAuthorizationMiddlewareResultHandler.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/User.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260425103000_AddUserRole.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/TaskTrackerDbContextModelSnapshot.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AccountControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Unit/JwtTokenServiceTests.cs
- task-tracker-web/src/app/shared/services/auth.service.spec.ts