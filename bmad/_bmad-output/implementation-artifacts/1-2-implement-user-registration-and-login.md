# Story 1.2: Implement User Registration and Login

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a visitor,
I want to create an account and log in securely,
So that I can access my personal task workspace.

## Acceptance Criteria

1. Given a visitor with valid credentials, when they submit registration, then a user account is created and confirmation response is returned, and password policies and duplicate-email validation are enforced.
2. Given a registered user, when they submit valid login credentials, then the API issues access and refresh tokens, and failed attempts return standardized Problem Details errors.

## Tasks / Subtasks

- [x] Design and implement User entity and database schema with password hashing (AC: 1)
  - [x] Create User domain model with email, password hash, salt, created/modified timestamps.
  - [x] Add User DbSet and migration for SQL Server database.
  - [x] Configure password hashing using PBKDF2 or bcrypt for secure storage.
  - [x] Add unique constraint on email field to enforce uniqueness.

- [x] Implement user registration API endpoint with validation (AC: 1)
  - [x] Create POST `/api/v1/auth/register` endpoint accepting email and password.
  - [x] Validate email format, duplicate email detection, and password policy (minimum length, complexity).
  - [x] Return Problem Details error responses for validation failures.
  - [x] Return 201 Created with user identifier and confirmation message on success.
  - [x] Add integration test for happy path and failure scenarios.

- [x] Implement user login API endpoint with token issuance (AC: 2)
  - [x] Create POST `/api/v1/auth/login` endpoint accepting email and password.
  - [x] Verify credentials against stored password hash using constant-time comparison.
  - [x] Issue access and refresh tokens (JWT format recommended with RS256 or HS256 signing).
  - [x] Configure token expiration: access token ~15 minutes, refresh token ~7 days.
  - [x] Return tokens in response body or secure HTTP-only cookies per security baseline.
  - [x] Add failed-attempt rate-limiting or logging baseline.
  - [x] Return Problem Details for invalid credentials, account not found, or other failures.
  - [x] Add integration test for successful login and failure scenarios.

- [x] Configure JWT token infrastructure and security baseline (AC: 2)
  - [x] Add JWT configuration to appsettings with issuer, audience, and key management.
  - [x] Register JWT authentication handler in dependency injection.
  - [x] Add token creation and validation service.
  - [x] Implement token signing and verification using stable key strategy.
  - [x] Document token lifecycle and renewal strategy for next story (1-3).

- [x] Add frontend registration and login UI forms (AC: 1, 2)
  - [x] Create registration form component with email, password, and confirm password fields.
  - [x] Add inline validation feedback for email format, password requirements, and field length.
  - [x] Create login form component with email and password fields and submit button.
  - [x] Add error message display for registration and login failures.
  - [x] Implement form submission to API endpoints and error state handling.
  - [x] Style forms using established design tokens and apply responsive mobile layout.

- [x] Wire frontend authentication state and routing (AC: 1, 2)
  - [x] Create Angular authentication service to manage login/logout and token storage.
  - [x] Add token persistence (localStorage or sessionStorage with security review).
  - [x] Create route guard to protect authenticated routes from unauthenticated access.
  - [x] Implement redirect from login/register to dashboard after successful authentication.
  - [x] Add logout action to clear stored tokens and redirect to login page.

- [x] Validate end-to-end registration and login flows (AC: 1, 2)
  - [x] Test frontend registration form submission with valid and invalid inputs.
  - [x] Test backend registration endpoint with duplicate email, weak password, and valid case.
  - [x] Test frontend login form submission with valid and invalid credentials.
  - [x] Test backend login endpoint with non-existent user and valid credentials.
  - [x] Verify tokens are issued and persisted correctly for authenticated requests.
  - [x] Verify login/register routes redirect to authenticated dashboard after success.
  - [x] Document any deviation from baseline auth architecture in Dev Notes below.

- [x] Add baseline quality gates for this story (AC: 1, 2)
  - [x] Add unit tests for password hashing, token generation, and validation logic.
  - [x] Add integration tests for registration and login endpoints with database isolation.
  - [x] Add frontend component unit tests for form validation and submission behavior.
  - [x] Ensure test coverage baseline is documented and reproducible locally.
  - [x] Add API documentation/Swagger comments for registration and login endpoints.

## Dev Notes

- Use industry-standard password hashing: bcrypt, Argon2, or PBKDF2 with salt. Do NOT store plaintext or unsalted hashes.
- Implement constant-time password comparison to prevent timing attacks.
- JWT tokens should be signed with a stable, private key. Use RS256 for asymmetric signing if available, else HS256 with a long key.
- Token storage strategy:
  - HTTP-only cookies are more secure but require CSRF protection on state-changing requests.
  - LocalStorage is simpler but vulnerable to XSS; use if frontend security practices are established.
  - Review and document choice in this file before implementation.
- Email uniqueness must be enforced at database level (unique constraint) and API level (validation).
- Rate limiting or attempt logging should be added at registration and login endpoints to mitigate brute force attacks.
- Problem Details responses for auth failures must be consistent with the error baseline from story 1.1.
- Ensure registration and login routes are publicly accessible (no authentication required) until after successful auth.
- Refresh token rotation and revocation strategy is deferred to story 1-3; this story focuses on access token issuance only.
- Store passwords with salt and hash; never store plain credentials.
- Session/token flows must support expiration, revocation, and secure renewal per NFR11.

### API Contracts

**Registration Request/Response:**
```
POST /api/v1/auth/register
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}

Response 201 Created:
{
  "userId": "uuid",
  "email": "user@example.com",
  "message": "Account created successfully"
}

Response 400 Bad Request (Problem Details):
{
  "type": "https://api.tasktracker.local/problems/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "Email already registered",
  "traceId": "0HN1FDHJ..."
}
```

**Login Request/Response:**
```
POST /api/v1/auth/login
{
  "email": "user@example.com",
  "password": "SecurePassword123!"
}

Response 200 OK:
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "eyJhbGc...",
  "expiresIn": 900
}

Response 401 Unauthorized (Problem Details):
{
  "type": "https://api.tasktracker.local/problems/authentication-failed",
  "title": "Authentication Failed",
  "status": 401,
  "detail": "Invalid email or password",
  "traceId": "0HN1FDHJ..."
}
```

### Project Structure Expectations

- Backend auth logic in `TaskTracker.Api/Controllers/AuthController.cs`.
- User entity in `TaskTracker.Api/Infrastructure/Persistence/Entities/User.cs` (or appropriate domain folder).
- JWT configuration in `TaskTracker.Api/appsettings.json` and `Program.cs`.
- Frontend auth service in `task-tracker-web/src/app/shared/services/auth.service.ts`.
- Login/register components in `task-tracker-web/src/app/features/auth/` or similar.
- Tests in `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs` and frontend component test files.

### References

- Story definition and ACs: [Source: _bmad-output/planning-artifacts/epics.md, Epic 1, Story 1.2]
- User and identity requirements: [Source: _bmad-output/planning-artifacts/prd.md, FR1, FR2, FR43, FR44]
- Security baseline and token lifetime: [Source: _bmad-output/planning-artifacts/architecture.md, Security Baseline; Token Lifecycle]
- Error contract: [Source: _bmad-output/planning-artifacts/architecture.md, API Error Contract (Problem Details)]
- Frontend auth service and route guard patterns: [Source: task-tracker-web/src/app/shared/services]
- Backend auth controller baseline: [Source: story 1-1 implementation context]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- dotnet build (TaskTracker.Api): success
- dotnet test --no-restore (TaskTracker.Api.Tests): success (7/7)
- dotnet dotnet-ef migrations add AddUserAuthentication: success
- npm run build (task-tracker-web): success
- npx ng test --watch=false --browsers=ChromeHeadless --no-progress: success (8/8)

### Completion Notes List

- Implemented registration and login APIs with Problem Details error responses and auth logging baseline.
- Added PBKDF2 password hashing with per-user salt and constant-time verification.
- Added JWT access/refresh token issuance and JWT bearer validation configuration.
- Implemented Users persistence model, unique email index, and SQL Server EF migration `AddUserAuthentication`.
- Implemented Angular auth service, login/register flows, route guard, protected dashboard route, and logout action.
- Chosen token persistence strategy for this story: localStorage (documented trade-off; CSRF-safe cookie flow deferred).
- Added backend unit/integration tests and frontend component tests, all passing locally.

### File List

- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/TaskTracker.Api/TaskTracker.Api.csproj
- task-tracker-api/TaskTracker.Api/appsettings.json
- task-tracker-api/TaskTracker.Api/appsettings.Development.json
- task-tracker-api/TaskTracker.Api/Controllers/AuthController.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/User.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260424123428_AddUserAuthentication.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260424123428_AddUserAuthentication.Designer.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/TaskTrackerDbContextModelSnapshot.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Contracts/AuthContracts.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Security/IPasswordHasher.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Security/Pbkdf2PasswordHasher.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Tokens/JwtOptions.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Tokens/IJwtTokenService.cs
- task-tracker-api/TaskTracker.Api/Features/Auth/Tokens/JwtTokenService.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/TaskTracker.Api.Tests.csproj
- task-tracker-api/tests/TaskTracker.Api.Tests/UnitTest1.cs
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/AuthControllerTests.cs
- task-tracker-web/src/app/app.config.ts
- task-tracker-web/src/app/app.html
- task-tracker-web/src/app/app.routes.ts
- task-tracker-web/src/app/app.spec.ts
- task-tracker-web/src/app/app.ts
- task-tracker-web/src/app/shared/services/auth.service.ts
- task-tracker-web/src/app/shared/guards/auth.guard.ts
- task-tracker-web/src/app/features/auth/login.component.ts
- task-tracker-web/src/app/features/auth/login.component.html
- task-tracker-web/src/app/features/auth/login.component.scss
- task-tracker-web/src/app/features/auth/login.component.spec.ts
- task-tracker-web/src/app/features/auth/register.component.ts
- task-tracker-web/src/app/features/auth/register.component.html
- task-tracker-web/src/app/features/auth/register.component.scss
- task-tracker-web/src/app/features/auth/register.component.spec.ts
- task-tracker-web/src/app/features/dashboard/dashboard.component.ts
- task-tracker-web/src/styles.scss
