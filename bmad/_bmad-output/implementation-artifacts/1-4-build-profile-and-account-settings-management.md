# Story 1.4: Build Profile and Account Settings Management

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to update profile and account preferences,
so that my identity and account experience match my needs.

## Acceptance Criteria

1. Given an authenticated user, when they update profile or settings fields, then only allowed fields are changed and persisted, and validation errors are returned with field-level detail.

## Tasks / Subtasks

- [x] Define profile and account-settings update boundaries and contracts (AC: 1)
  - [x] Introduce explicit API contracts for retrieving and updating current-user profile/settings under `/api/v1` (for example, `GET /api/v1/account/me`, `PATCH /api/v1/account/profile`, `PATCH /api/v1/account/settings`).
  - [x] Define an allowlist of mutable fields and reject unknown or restricted fields to prevent over-posting and privilege escalation.
  - [x] Keep response and error payloads aligned with RFC 7807 Problem Details plus stable `code` and `traceId` extension fields.
  - [x] Keep this story scoped to profile/account preferences only; notification preferences and leaderboard privacy participation controls are handled in later epics.

- [x] Extend persistence model for profile/settings with SQL Server migration (AC: 1)
  - [x] Add profile/settings fields to the user aggregate in the existing persistence slice (for example: display name, timezone, locale, or equivalent approved fields).
  - [x] Configure EF Core mappings, constraints, and max lengths to enforce domain limits at database level.
  - [x] Create a migration in `Infrastructure/Persistence/Migrations` and verify the schema applies cleanly on SQL Server.
  - [x] Ensure `ModifiedAtUtc` (or equivalent audit timestamp) updates deterministically on profile/settings change.

- [x] Implement authenticated profile/settings endpoints with strict ownership rules (AC: 1)
  - [x] Require authenticated access for all profile/settings operations and resolve target user from JWT claims instead of client-provided user IDs.
  - [x] Implement update handlers that map only allowlisted fields and preserve immutable/security-sensitive fields (email, password hash/salt, roles, auth session state).
  - [x] Return field-level validation details for invalid inputs (for example, invalid timezone identifier, length violations, malformed format).
  - [x] Emit structured logs for update attempts, success, and validation/auth failures with trace correlation.

- [x] Build frontend profile/settings UX and API wiring (AC: 1)
  - [x] Add a profile/settings UI surface in the existing Angular app (dashboard-linked route or account panel) without breaking current auth and dashboard navigation.
  - [x] Use reactive forms with inline validation messages and preserve user input on validation failure.
  - [x] Integrate with new account endpoints through a dedicated shared service while keeping existing auth service responsibilities focused on session lifecycle.
  - [x] Keep keyboard operability, focus-visible behavior, and responsive parity between desktop and mobile breakpoints.

- [x] Add backend and frontend automated tests for allowed updates and validation failures (AC: 1)
  - [x] Backend integration tests: authenticated success update, unauthenticated/forbidden access, unknown field rejection, and validation failure with Problem Details field errors.
  - [x] Backend unit tests: allowlist mapping logic and field validators.
  - [x] Frontend unit tests: form validation display, successful save flow, and recoverable error rendering.
  - [x] Re-run auth regression tests from stories 1.2 and 1.3 to ensure profile/settings work does not break registration/login/refresh/logout behavior.

## Dev Notes

- Extend the existing implementation instead of introducing a parallel identity stack. Current auth baseline lives in `TaskTracker.Api/Controllers/AuthController.cs`, `TaskTracker.Api/Features/Auth/Repositories/`, `TaskTracker.Api/Infrastructure/Persistence/Entities/User.cs`, and `task-tracker-web/src/app/shared/services/auth.service.ts`.
- Current `User` entity contains auth-centric fields only (`Email`, password hash/salt, timestamps). This story introduces user-manageable profile/settings data in the same persistence model and should not alter credential semantics.
- Keep API base under `/api/v1` and continue Problem Details consistency (`type`, `title`, `status`, `code`, `traceId`, and `errors` for validation).
- Enforce server-side ownership for profile/settings updates by deriving current user identity from validated access token claims; never trust user IDs from request payload.
- Preserve existing architecture boundaries: external API logic in controllers/contracts, persistence concerns in infrastructure, and avoid broad modular refactors in this story.
- SQL Server remains mandatory for data persistence and migration workflows.
- UX expectations require clear form guidance, field-level errors, keyboard accessibility, and responsive behavior consistent with existing auth screens.

### API Contracts

**Profile/Settings Read:**
```
GET /api/v1/account/me
Authorization: Bearer <access-token>

Response 200 OK:
{
  "userId": "guid",
  "email": "user@example.com",
  "displayName": "User Name",
  "timeZoneId": "Europe/Kyiv",
  "locale": "en-US",
  "updatedAtUtc": "2026-04-24T13:40:00Z"
}
```

**Profile Update:**
```
PATCH /api/v1/account/profile
Authorization: Bearer <access-token>
{
  "displayName": "User Name"
}

Response 200 OK:
{
  "message": "Profile updated successfully"
}
```

**Settings Update:**
```
PATCH /api/v1/account/settings
Authorization: Bearer <access-token>
{
  "timeZoneId": "Europe/Kyiv",
  "locale": "en-US"
}

Response 400 Bad Request (Problem Details):
{
  "type": "https://api.tasktracker.local/problems/validation-error",
  "title": "Validation Error",
  "status": 400,
  "code": "account.settings.validation_failed",
  "traceId": "0HN1FDHJ...",
  "errors": {
    "timeZoneId": ["The selected timezone is not valid."]
  }
}
```

### Previous Story Intelligence

- Story 1.3 established server-side session authority (refresh rotation/revocation) and added an auth interceptor in Angular. Reuse current auth context for account endpoints rather than adding duplicate token handling.
- Story 1.2 established user persistence, JWT auth wiring, and Problem Details patterns. Continue those conventions for account-profile APIs and validation behavior.
- Existing auth and dashboard flows are working and covered by tests; this story must avoid regressions in login/register/refresh/logout routing and token behavior.

### Project Structure Notes

- Expected backend touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/`
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Contracts/` (or a new account/profile contracts slice aligned with current feature layout)
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Repositories/` (or adjacent account repository abstraction)
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/User.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`
- Expected frontend touch points:
  - `task-tracker-web/src/app/features/dashboard/` (entry link or account surface integration)
  - `task-tracker-web/src/app/features/` (new profile/settings feature files if introduced)
  - `task-tracker-web/src/app/shared/services/` (account-profile API service)
  - `task-tracker-web/src/app/shared/guards/auth.guard.ts`
  - `task-tracker-web/src/app/app.routes.ts`

### Testing Requirements

- Backend integration tests should continue using existing auth test infrastructure and SQL Server-compatible EF Core test setup.
- Validate unauthorized access returns auth-consistent Problem Details and trace information.
- Validate invalid fields produce deterministic, field-level validation errors (not generic error strings).
- Frontend tests should verify keyboard-accessible form interactions and persistence of typed values on validation failure.

### References

- Story definition and ACs: [Source: _bmad-output/planning-artifacts/epics.md, Epic 1, Story 1.4]
- Profile/settings and ownership requirements: [Source: _bmad-output/planning-artifacts/prd.md, FR3, FR4, FR27; Authorization and Access Control]
- Authz and API/error contract guardrails: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; API Design and Integration Style; Security and Compliance]
- UX form and accessibility requirements: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Form Patterns; Accessibility Strategy; Responsive Strategy]
- Existing implementation baseline: [Source: _bmad-output/implementation-artifacts/1-3-implement-secure-session-lifecycle-and-logout.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Ultimate context engine analysis completed - comprehensive developer guide created.

### File List

- _bmad-output/implementation-artifacts/1-4-build-profile-and-account-settings-management.md
