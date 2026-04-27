# Story 1.1: Initialize Solution from Selected Starter Template

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a developer,
I want to scaffold the Angular web app and ASP.NET Core API with SQL Server wiring,
so that all later stories build on the approved architecture baseline.

## Acceptance Criteria

1. Given an empty repository root, when starter commands are run for Angular and ASP.NET Core projects and solution wiring, then `task-tracker-web` and `task-tracker-api` are created with buildable defaults.
2. SQL Server EF Core provider and migration scaffolding baseline are configured.

## Tasks / Subtasks

- [x] Scaffold the frontend and backend projects from the selected starter stack (AC: 1)
  - [x] Create Angular web app in `task-tracker-web` using strict TypeScript and SCSS.
  - [x] Create ASP.NET Core .NET 9 Web API in `task-tracker-api/TaskTracker.Api`.
  - [x] Create `TaskTracker.sln` and add backend project.
- [x] Establish data-platform baseline for SQL Server in API project (AC: 2)
  - [x] Add EF Core SQL Server and EF Core Design packages.
  - [x] Add initial DbContext registration with SQL Server provider and retry policy baseline.
  - [x] Add first migration scaffold placeholder and verify migration tooling execution path.
- [x] Validate baseline build and run paths for both apps (AC: 1, 2)
  - [x] Run frontend install/build and confirm default app compiles.
  - [x] Run backend restore/build and confirm API starts with OpenAPI enabled in development.
  - [x] Confirm solution and folder layout matches architecture boundaries.
- [x] Add baseline quality gates for this story (AC: 1, 2)
  - [x] Add backend test project placeholder (unit/integration split can be expanded in next stories).
  - [x] Ensure frontend unit test scaffold remains operational after setup.
  - [x] Document local setup/run commands in root README for developer handoff.

## Dev Notes

- Use the selected starter exactly: Angular CLI + ASP.NET Core .NET 9 Web API, with SQL Server via `Microsoft.EntityFrameworkCore.SqlServer`.
- Keep architecture boundaries from day one:
  - Backend structure anchored around Api/Application/Domain/Infrastructure modules.
  - Frontend feature-first structure under `src/app/features`, with shared code under `src/app/shared`.
- API baseline must be consistent with future stories:
  - Versioned route base `/api/v1`.
  - Problem Details-compatible error shape with trace correlation support.
- Naming and file conventions must be respected immediately to avoid later refactors:
  - Angular files in kebab-case.
  - C# files/types in PascalCase.
  - API JSON fields camelCase.
- Keep this story scoped to scaffolding and baseline wiring only. Do not implement auth/business features yet.

### Project Structure Notes

- Expected top-level artifacts from this story:
  - `TaskTracker.sln`
  - `task-tracker-web/`
  - `task-tracker-api/TaskTracker.Api/`
- Alignment target is the architecture-defined structure that later adds `TaskTracker.Application`, `TaskTracker.Domain`, `TaskTracker.Infrastructure`, and dedicated test projects.
- Any structural deviation in this setup story must be documented in this file before implementation proceeds.

### References

- Story definition and ACs: [Source: _bmad-output/planning-artifacts/epics.md, Epic 1, Story 1.1]
- Mandatory starter and SQL Server requirement: [Source: _bmad-output/planning-artifacts/epics.md, Additional Requirements]
- Starter commands and stack decisions: [Source: _bmad-output/planning-artifacts/architecture.md, Selected Starter: Angular CLI + ASP.NET Core Web API (separate projects)]
- Data platform baseline and migration strategy: [Source: _bmad-output/planning-artifacts/architecture.md, Data Platform Preference (Captured)]
- Naming, structure, and test organization conventions: [Source: _bmad-output/planning-artifacts/architecture.md, Naming Patterns; Project Structure and Boundaries]
- Responsive web and UX baseline intent: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, Core User Experience; Platform Strategy]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Angular app scaffolded under `task-tracker-web` using strict TypeScript, SCSS, and routing with CSR baseline (SSR/SSG disabled).
- ASP.NET Core .NET 9 Web API scaffolded in `task-tracker-api/TaskTracker.Api` and added to `TaskTracker.sln`.
- EF Core SQL Server baseline configured with `Microsoft.EntityFrameworkCore.SqlServer` and `Microsoft.EntityFrameworkCore.Design` pinned to `9.0.6` for .NET 9 compatibility.
- SQL Server DbContext registration and retry-on-failure baseline added in API startup.
- Initial EF migration scaffolded to `Infrastructure/Persistence/Migrations` and local `dotnet-ef` tool manifest created at `.config/dotnet-tools.json`.
- Backend test placeholder project created in `task-tracker-api/tests/TaskTracker.Api.Tests` and added to solution.
- Frontend structure placeholders created for `src/app/features` and `src/app/shared`.
- Verification executed successfully: Angular build/test pass, .NET restore/build/test pass, API run and OpenAPI endpoint returns HTTP 200.
- Root README added with local setup, run, test, and migration commands.
- Story status advanced to `done`.

### File List

- .config/dotnet-tools.json
- .gitignore
- README.md
- TaskTracker.sln
- _bmad-output/implementation-artifacts/1-1-initialize-solution-from-selected-starter-template.md
- task-tracker-api/TaskTracker.Api/Controllers/WeatherForecastController.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260424113702_InitialBaseline.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/20260424113702_InitialBaseline.Designer.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/TaskTrackerDbContextModelSnapshot.cs
- task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs
- task-tracker-api/TaskTracker.Api/Program.cs
- task-tracker-api/TaskTracker.Api/TaskTracker.Api.csproj
- task-tracker-api/TaskTracker.Api/appsettings.Development.json
- task-tracker-api/TaskTracker.Api/appsettings.json
- task-tracker-api/tests/TaskTracker.Api.Tests/TaskTracker.Api.Tests.csproj
- task-tracker-api/tests/TaskTracker.Api.Tests/Unit/.gitkeep
- task-tracker-api/tests/TaskTracker.Api.Tests/Integration/.gitkeep
- task-tracker-web/src/app/features/.gitkeep
- task-tracker-web/src/app/shared/.gitkeep