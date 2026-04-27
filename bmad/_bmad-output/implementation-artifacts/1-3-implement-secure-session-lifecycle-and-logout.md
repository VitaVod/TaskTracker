# Story 1.3: Implement Secure Session Lifecycle and Logout

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authenticated user,
I want secure token renewal and logout,
so that my session remains safe and controllable.

## Acceptance Criteria

1. Given a valid refresh token, when token refresh is requested, then a new access token is issued and old token state is rotated or revoked per policy, and token expiration and revocation behaviors are auditable.
2. Given an authenticated session, when logout is requested, then the active refresh token is revoked, and subsequent API calls with old session material are rejected.

## Tasks / Subtasks

- [x] Persist server-side session state for refresh-token rotation and revocation (AC: 1, 2)
  - [x] Add a persistence model for refresh-session state in the existing API project, including user linkage, token identifier or fingerprint, issued/expiry timestamps, revoked timestamp, replacement linkage, and audit metadata needed to explain rotation and revocation decisions.
  - [x] Update `TaskTrackerDbContext` and create an EF Core migration for the new session storage using SQL Server naming conventions.
  - [x] Keep refresh-token secrets out of plaintext storage; persist only the minimum material needed to validate, revoke, and audit server-side session state.
  - [x] Define explicit server-side policy for single-use refresh tokens and token-family revocation so rotated or revoked tokens cannot be replayed.

- [x] Implement refresh-token exchange with deterministic rotation rules (AC: 1)
  - [x] Add `POST /api/v1/auth/refresh` request and response contracts under the existing auth contracts slice.
  - [x] Validate token type, signature, expiry, and server-side session state before issuing a new token pair.
  - [x] Revoke or rotate the previously presented refresh token inside the same persistence transaction that records the new token state.
  - [x] Return RFC 7807 Problem Details with stable app error codes and trace ID for expired, revoked, malformed, or replayed refresh tokens.
  - [x] Emit structured audit or security log entries for successful refresh, rejected refresh, and replay or revocation events.

- [x] Implement authenticated logout with server-side revocation enforcement (AC: 2)
  - [x] Add `POST /api/v1/auth/logout` and require authenticated access-token context for the caller.
  - [x] Revoke the active refresh session and any required related token-family state so the logged-out session cannot be renewed.
  - [x] Extend protected-token validation so access tokens tied to a revoked session are rejected after logout instead of remaining valid solely because their JWT expiry has not elapsed.
  - [x] Return a deterministic success response for idempotent repeat logout requests while still preserving revocation state and audit traceability.

- [x] Wire frontend session lifecycle behavior to the backend auth flow (AC: 1, 2)
  - [x] Extend `AuthService` with backend-backed refresh and logout operations instead of local token deletion only.
  - [x] Add an HTTP interceptor or equivalent shared API hook that attaches the current access token and performs at most one refresh attempt before redirecting to login.
  - [x] Prevent refresh loops, concurrent duplicate refresh requests, and stale-token overwrites in client storage.
  - [x] Preserve existing auth form and dashboard behavior while adding clear, recoverable messaging when a session expires or is revoked.
  - [x] Keep mobile and desktop behavior aligned and maintain keyboard-accessible logout and session-expiry flows.

- [x] Add automated coverage for refresh, revocation, and logout edge cases (AC: 1, 2)
  - [x] Add backend integration tests for successful refresh, expired refresh token, revoked refresh token, replayed refresh token, successful logout, and post-logout rejection of old session material.
  - [x] Add backend unit tests for token rotation, session validation, and revocation decision logic.
  - [x] Add frontend unit tests for auth service refresh and logout flows, including session-expiry handling and redirect behavior.
  - [x] Verify `dotnet test` and Angular auth-related tests remain reproducible without introducing flaky time-based assertions.

## Dev Notes

- Extend the current auth implementation instead of creating a parallel authentication stack. The existing entry points are `TaskTracker.Api/Controllers/AuthController.cs`, `TaskTracker.Api/Features/Auth/Repositories/AuthRepository.cs`, `TaskTracker.Api/Features/Auth/Tokens/JwtTokenService.cs`, `TaskTracker.Api/Program.cs`, and `task-tracker-web/src/app/shared/services/auth.service.ts`.
- Story 1.2 already issues access and refresh JWTs, but refresh tokens are not persisted or revocable yet. This story should close that gap rather than replace JWT usage.
- Current protected-endpoint validation in `Program.cs` only enforces `token_type == access`. To satisfy AC 2, add server-side session or revocation validation on protected requests so a logged-out session cannot continue using old access tokens until natural expiry.
- Keep API contracts under `/api/v1` and continue using camelCase JSON fields.
- Use Problem Details consistently for auth failures. Architecture requires `type`, `title`, `status`, `code`, `traceId`, and validation `errors` when applicable. The current project adds `traceId`; this story should add stable auth error codes instead of ad-hoc payloads.
- Current repo structure is still concentrated in `TaskTracker.Api`; do not expand this story into a broad modular-monolith refactor. Place new files where they fit the existing auth and persistence slices while keeping names and seams compatible with the target architecture.
- SQL Server remains the required data platform. Keep migrations in the persistence area and follow existing EF Core patterns established in stories 1.1 and 1.2.
- Refresh and logout operations are security-sensitive. Favor deterministic, server-authoritative behavior over optimistic client behavior.
- Frontend currently uses plain Angular HTTP services and guards, not Angular Material. Keep UX changes scoped to session lifecycle messaging and controls already touched by the auth flow.

### API Contracts

**Refresh Request/Response:**
```
POST /api/v1/auth/refresh
{
  "refreshToken": "eyJhbGc..."
}

Response 200 OK:
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 900
}

Response 401 Unauthorized (Problem Details):
{
  "type": "https://api.tasktracker.local/problems/session-invalid",
  "title": "Session Invalid",
  "status": 401,
  "code": "auth.session.invalid",
  "detail": "The session is expired, revoked, or no longer valid.",
  "traceId": "0HN1FDHJ..."
}
```

**Logout Request/Response:**
```
POST /api/v1/auth/logout
Authorization: Bearer <access-token>
{
  "refreshToken": "eyJhbGc..."
}

Response 200 OK:
{
  "message": "Session revoked successfully"
}
```

### Previous Story Intelligence

- Story 1.2 established the current auth baseline: PBKDF2 password hashing, JWT access and refresh issuance, Problem Details-based auth failures, integration tests through `AuthControllerTests`, and Angular auth state stored in `localStorage`.
- Reuse the existing auth repository and token service patterns where practical, but avoid leaving refresh logic as a pure token-generation concern; revocation decisions need persistence-backed validation.
- Story 1.2 explicitly deferred refresh rotation and revocation to this story. Treat that deferral as a hard dependency rather than optional enhancement.
- Current frontend logout only clears local storage. Replace it with server-backed revocation plus local cleanup, but keep redirect behavior consistent with the current login and guard flow.

### Project Structure Notes

- Expected backend touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/AuthController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Contracts/AuthContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Repositories/`
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Tokens/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs`
- Expected frontend touch points:
  - `task-tracker-web/src/app/shared/services/auth.service.ts`
  - `task-tracker-web/src/app/shared/guards/auth.guard.ts`
  - `task-tracker-web/src/app/app.config.ts` or equivalent shared HTTP provider registration point for an interceptor
  - existing auth feature files under `task-tracker-web/src/app/features/auth/` only if session-expiry/logout messaging needs UI changes
- Avoid creating cross-feature auth helpers outside the shared auth/API slice.

### Testing Requirements

- Backend integration tests should continue to use the `AuthTestFactory` pattern from story 1.2.
- Prefer deterministic test control around time-sensitive token behavior; if a clock abstraction becomes necessary for reliable expiry tests, keep it narrowly scoped to auth/session logic.
- Frontend tests should verify that failed refresh attempts clear stale tokens and route users back to login without infinite retry behavior.
- Re-run the focused auth test slice before broader builds: backend auth tests first, then frontend auth unit tests, then full build if needed.

### References

- Story definition and ACs: [Source: _bmad-output/planning-artifacts/epics.md, Epic 1, Story 1.3]
- Security and token lifecycle requirements: [Source: _bmad-output/planning-artifacts/prd.md, Security]
- Session expiration, revocation, and secure renewal baseline: [Source: _bmad-output/planning-artifacts/prd.md, NFR11 / Additional Requirements]
- Auth, Problem Details, and structure patterns: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; Naming Patterns; Format Patterns; Project Structure and Boundaries]
- Frontend accessibility and recoverable error guidance: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Platform Strategy; Form Patterns; Accessibility and Compliance]
- Current implementation baseline: [Source: _bmad-output/implementation-artifacts/1-2-implement-user-registration-and-login.md]

## Dev Agent Record

### Agent Model Used

GPT-5.4

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Implemented refresh-session persistence with rotation and revocation metadata in SQL Server-backed EF Core models and migrations.
- Implemented `POST /api/v1/auth/refresh` and `POST /api/v1/auth/logout` with deterministic session-state validation, rotation, and idempotent revocation behavior.
- Extended protected-request token validation to enforce server-side session revocation after logout.
- Added Problem Details-based auth failure responses with stable error codes and `traceId`.
- Implemented frontend refresh/logout flows with interceptor-based single-refresh behavior and loop prevention.
- Added backend and frontend automated tests for refresh, replay, revocation, logout, and post-logout rejection paths.

### File List

- _bmad-output/implementation-artifacts/1-3-implement-secure-session-lifecycle-and-logout.md
