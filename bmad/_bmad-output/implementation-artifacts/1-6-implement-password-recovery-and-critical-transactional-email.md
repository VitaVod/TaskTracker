# Story 1.6: Implement Password Recovery and Critical Transactional Email

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want to recover account access through secure email flows,
so that I can regain access when credentials are lost.

## Acceptance Criteria

1. Given a registered email, when password recovery is requested, then a time-limited, single-use recovery link is issued, and delivery is routed through transactional email service with retry policy.
2. Given a recovery link is reused or expired, when reset is attempted, then reset is rejected with explicit recovery guidance, and security events are logged.

## Tasks / Subtasks

- [x] Add password recovery domain model and persistence baseline (AC: 1, 2)
  - [x] Add a recovery token entity (or equivalent) with fields for `tokenId`, `userId`, `issuedAtUtc`, `expiresAtUtc`, `usedAtUtc`, `revokedAtUtc`, `deliveryAttemptCount`, and `lastDeliveryAttemptAtUtc`.
  - [x] Store only a hashed token value (never plaintext) and add indexes for lookup and expiry cleanup.
  - [x] Add EF Core migration(s) and update `TaskTrackerDbContext` mappings under existing persistence conventions.

- [x] Implement forgot-password request flow with anti-enumeration and retry-safe delivery orchestration (AC: 1)
  - [x] Add `POST /api/v1/auth/password-recovery/request` endpoint and contracts in the auth feature.
  - [x] Normalize email and, regardless of account existence, return a deterministic success-style response to prevent account enumeration.
  - [x] For known users, create a new time-limited, single-use token and enqueue or trigger transactional email delivery with monitored retry handling for transient failures.

- [x] Implement reset-password confirmation flow with single-use and expiry enforcement (AC: 1, 2)
  - [x] Add `POST /api/v1/auth/password-recovery/confirm` endpoint and contracts that accept recovery token and new password.
  - [x] Validate token integrity, expiry window, and one-time usage, then rotate password hash/salt and mark token consumed atomically.
  - [x] Revoke active refresh sessions for that user after successful reset to force re-authentication on all devices.

- [x] Standardize recovery error contracts and guidance responses (AC: 2)
  - [x] Return RFC 7807 Problem Details with stable `type`, `title`, `status`, `code`, and `traceId` for invalid/expired/reused token outcomes.
  - [x] Ensure reused/expired token responses include explicit next-step guidance (request a new link).
  - [x] Keep business-failure responses non-retriable while allowing retry only for transient delivery infrastructure failures.

- [x] Add security-event and delivery observability logs (AC: 2)
  - [x] Log recovery request accepted events, token issuance, token rejection reasons (expired/reused/invalid), and successful password resets with trace correlation.
  - [x] Log transactional email delivery attempts/results and retry outcomes without leaking token values or secrets.
  - [x] Ensure logs are structured for future support/admin operational tooling in epics 5 and 6.

- [x] Add frontend recovery UX and auth service support (AC: 1, 2)
  - [x] Add password recovery request and reset pages/components under auth feature routes.
  - [x] Add auth service methods for request and confirm endpoints and keep existing session lifecycle behavior unchanged.
  - [x] Ensure UX feedback aligns with product guidance: clear, action-oriented, and non-enumerating for request flow.

- [x] Add backend and frontend tests plus regressions (AC: 1, 2)
  - [x] Backend integration tests for recovery request, successful reset, expired token, reused token, and deterministic Problem Details contract shape.
  - [x] Backend tests that verify session revocation after successful password reset.
  - [x] Frontend tests for request/reset forms, success and guidance messaging, and API-error handling paths.
  - [x] Re-run stories 1.2-1.5 auth/account regression paths (register, login, refresh, logout, account updates, role gates).

## Dev Notes

- Build on existing auth foundation in `TaskTracker.Api/Controllers/AuthController.cs`, `TaskTracker.Api/Features/Auth/Repositories/AuthRepository.cs`, and auth contracts under `TaskTracker.Api/Features/Auth/Contracts/AuthContracts.cs`.
- Continue using SQL Server via EF Core and follow existing migration workflow in `TaskTracker.Api/Infrastructure/Persistence/Migrations/`.
- Keep API surface versioned under `/api/v1` and maintain RFC 7807 error contracts with stable app `code` plus `traceId`.
- Avoid token leakage: do not log raw recovery tokens, do not expose whether email exists, and keep security-sensitive outcomes traceable.
- Story 1.3 and 1.5 established session authority and authorization contract consistency; password reset must not bypass these controls.

### API Contracts

**Password recovery request (anti-enumeration response):**
```
POST /api/v1/auth/password-recovery/request
Content-Type: application/json

{
  "email": "user@example.com"
}

HTTP/1.1 202 Accepted
Content-Type: application/json

{
  "message": "If the account exists, a recovery email has been sent."
}
```

**Password reset confirmation success:**
```
POST /api/v1/auth/password-recovery/confirm
Content-Type: application/json

{
  "token": "<recovery-token>",
  "newPassword": "StrongPass123!"
}

HTTP/1.1 200 OK
Content-Type: application/json

{
  "message": "Password updated successfully"
}
```

**Expired or reused recovery token contract:**
```
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://api.tasktracker.local/problems/password-recovery-invalid",
  "title": "Recovery Link Invalid",
  "status": 400,
  "code": "auth.password-recovery.invalid",
  "traceId": "0HN1FDHJ...",
  "detail": "This recovery link is expired or already used. Request a new recovery email."
}
```

### Previous Story Intelligence

- Story 1.5 introduced centralized authorization/error handling with stable `code` and `traceId` contracts. Reuse that standard for recovery endpoints and failure paths.
- Story 1.5 also introduced role and denial logging patterns; apply equivalent structured logging discipline for security-sensitive recovery events.
- Story 1.4 profile/settings updates demonstrate ownership-safe mutation and explicit validation feedback conventions; keep the same deterministic error shape.
- Story 1.3 established server-authoritative session lifecycle and refresh replay controls. On successful password reset, session invalidation must preserve this trust boundary.

### Git Intelligence Summary

- Latest commit history currently shows one baseline commit (`Task Tracker creation`), so existing story artifacts and current codebase patterns are the primary source of implementation conventions.

### Project Structure Notes

- Expected backend touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/AuthController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Contracts/AuthContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Repositories/IAuthRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Auth/Repositories/AuthRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/TaskTracker.Api/Program.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Unit/`

- Expected frontend touch points:
  - `task-tracker-web/src/app/app.routes.ts`
  - `task-tracker-web/src/app/features/auth/`
  - `task-tracker-web/src/app/shared/services/auth.service.ts`
  - `task-tracker-web/src/app/shared/interceptors/`

### Testing Requirements

- Verify request endpoint always returns anti-enumeration-safe response regardless of account existence.
- Verify recovery tokens are time-limited, single-use, and rejected when expired/replayed.
- Verify successful reset rotates password material and revokes active refresh sessions.
- Verify Problem Details shape for all recovery error outcomes includes `code` and `traceId`.
- Verify transactional delivery retry behavior handles transient failures and does not retry business-rule failures.
- Verify frontend request/reset flows provide explicit user guidance and preserve auth/session stability.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 1, Story 1.6]
- Functional and non-functional requirements (`FR43`, `FR44`, `FR48`, `NFR12`, `NFR18`): [Source: _bmad-output/planning-artifacts/epics.md, Functional Requirements and NonFunctional Requirements]
- Product requirement details for secure recovery links and monitored delivery: [Source: _bmad-output/planning-artifacts/prd.md, FR43/FR48 and NFR14/NFR31]
- Architecture constraints for API error contracts, SQL retry policy, and email integration adapter direction: [Source: _bmad-output/planning-artifacts/architecture.md, API and Communication Patterns; Process Patterns; Integration Points]
- UX guidance for action-oriented feedback and accessibility baseline: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Core User Experience; Accessibility Considerations]
- Previous story implementation baseline: [Source: _bmad-output/implementation-artifacts/1-5-implement-role-policies-and-ownership-authorization-baseline.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Added password recovery token persistence with hashed token storage, lookup/cleanup indexes, and migration.
- Implemented anti-enumeration password recovery request endpoint and retry-aware transactional email delivery orchestration.
- Implemented password recovery confirmation endpoint with token expiry/single-use checks, password rotation, and active refresh-session revocation.
- Standardized invalid/expired/reused recovery outcomes to RFC 7807 contracts with stable `code` and `traceId` plus explicit next-step guidance.
- Added structured security and delivery logs for request acceptance, issuance, rejection outcomes, reset success, and retry attempts.
- Added frontend forgot/reset password UX flows and auth service methods for recovery request/confirm APIs.
- Added backend integration coverage for deterministic request responses, retry tracking, reset success, expired/reused token guidance contracts, and session revocation.
- Added frontend unit coverage for forgot/reset components and auth recovery service calls.
- Validation: `dotnet test` passed for backend and `npx ng test --watch=false --browsers=ChromeHeadless` plus `npm run build` passed for frontend.

### File List

- task-tracker-api/TaskTracker.Api/Controllers/AuthController.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Contracts/AuthContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Email/ITransactionalEmailService.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Email/LoggingTransactionalEmailService.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Repositories/IAuthRepository.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Repositories/AuthRepository.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/PasswordRecoveryToken.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260425084057_AddPasswordRecovery.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260425084057_AddPasswordRecovery.Designer.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/TaskTrackerDbContextModelSnapshot.cs
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-web/src/app/app.routes.ts
- task-tracker-web/src/app/features/auth/index.ts
- task-tracker-web/src/app/features/auth/login.component.html
- task-tracker-web/src/app/features/auth/forgot-password.component.ts
- task-tracker-web/src/app/features/auth/forgot-password.component.html
- task-tracker-web/src/app/features/auth/forgot-password.component.scss
- task-tracker-web/src/app/features/auth/forgot-password.component.spec.ts
- task-tracker-web/src/app/features/auth/reset-password.component.ts
- task-tracker-web/src/app/features/auth/reset-password.component.html
- task-tracker-web/src/app/features/auth/reset-password.component.scss
- task-tracker-web/src/app/features/auth/reset-password.component.spec.ts
- task-tracker-web/src/app/shared/services/auth.service.ts
- task-tracker-web/src/app/shared/services/auth.service.spec.ts
- _bmad-output/implementation-artifacts/1-6-implement-password-recovery-and-critical-transactional-email.md
