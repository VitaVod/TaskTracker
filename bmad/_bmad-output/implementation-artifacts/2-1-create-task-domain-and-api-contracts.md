# Story 2.1: Create Task Domain and API Contracts

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As an authenticated user,
I want to create tasks with essential attributes,
so that I can capture work items quickly.

## Acceptance Criteria

1. Given valid task input, when create task is submitted, then task is stored under requesting user ownership.
2. Response includes normalized task payload for immediate UI rendering.
3. Given invalid task input, when create task is submitted, then validation errors return in Problem Details format.
4. No task is created for invalid input.

## Tasks / Subtasks

- [x] Add task domain entity and persistence mappings (AC: 1, 4)
  - [x] Add task entity under persistence/domain conventions with required ownership field (`userId`) and essential attributes (for example `title`, `description`, `dueAtUtc`, `priority`, `category`, `isCompleted`, `createdAtUtc`, `updatedAtUtc`).
  - [x] Configure EF Core mappings, constraints, and indexes for user-owned task lookup and common list ordering.
  - [x] Add and apply EF Core migration for the task table and related indexes in SQL Server.

- [x] Implement create-task application workflow and repository contract (AC: 1, 2, 4)
  - [x] Add repository abstraction and implementation methods for creating user-owned tasks.
  - [x] Enforce ownership assignment from authenticated principal (never from client payload).
  - [x] Normalize response model fields to match frontend expectations and API naming conventions.

- [x] Add versioned create-task endpoint and contracts (AC: 1, 2, 3, 4)
  - [x] Add `POST /api/v1/tasks` endpoint with request/response DTOs in task feature area.
  - [x] Validate required fields and business constraints at API boundary and/or application layer.
  - [x] Return RFC 7807 Problem Details with stable app error `code` and `traceId` for validation failures.

- [x] Integrate authz and ownership baseline for task creation (AC: 1)
  - [x] Require authenticated user context for create endpoint.
  - [x] Ensure server-side ownership is derived from token/session identity.
  - [x] Confirm unauthorized requests receive consistent auth error contract.

- [x] Add backend tests for create-task happy and validation paths (AC: 1, 2, 3, 4)
  - [x] Integration test for successful create returns normalized payload and persists under caller ownership.
  - [x] Integration tests for invalid payloads returning Problem Details shape with `code` and `traceId`.
  - [x] Regression test ensuring client cannot impersonate another user via payload fields.

- [x] Add frontend contract integration for create flow baseline (AC: 2, 3)
  - [x] Add task API service method for create operation aligned with response contract.
  - [x] Add or update create-task form model typing to match request DTO.
  - [x] Add unit tests for request mapping and API error handling behavior.

## Dev Notes

- Story 1 established auth/session and ownership enforcement foundations. Reuse existing identity extraction and authorization patterns for task creation.
- Keep API routes versioned under `/api/v1` and preserve RFC 7807-compatible error contracts with stable app `code` plus `traceId`.
- Data platform remains SQL Server via EF Core; follow existing migration and DbContext registration conventions.
- Task creation is an ownership-sensitive write path: ownership must be server-authoritative and non-overridable by user payload.
- Normalize returned payload so the UI can render a new task immediately without shape transformation logic.

### API Contracts

**Create task request/response baseline:**
```
POST /api/v1/tasks
Content-Type: application/json
Authorization: Bearer <access-token>

{
  "title": "Plan sprint backlog",
  "description": "Draft story priorities for next sprint",
  "dueAtUtc": "2026-04-27T18:00:00Z",
  "priority": "medium",
  "category": "planning"
}

HTTP/1.1 201 Created
Content-Type: application/json

{
  "id": "7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12",
  "title": "Plan sprint backlog",
  "description": "Draft story priorities for next sprint",
  "dueAtUtc": "2026-04-27T18:00:00Z",
  "priority": "medium",
  "category": "planning",
  "isCompleted": false,
  "createdAtUtc": "2026-04-25T11:30:12Z",
  "updatedAtUtc": "2026-04-25T11:30:12Z"
}
```

**Validation failure contract example:**
```
HTTP/1.1 400 Bad Request
Content-Type: application/problem+json

{
  "type": "https://api.tasktracker.local/problems/validation",
  "title": "Validation failed",
  "status": 400,
  "code": "validation.request.invalid",
  "traceId": "0HN1FDHJ...",
  "errors": {
    "title": ["The title field is required."]
  }
}
```

### Previous Story Intelligence

- Story 1.5 established role policies and ownership enforcement conventions; task creation must preserve server-side ownership assignment and authorization contract consistency.
- Story 1.6 reinforced Problem Details and observability expectations; reuse stable `code` and `traceId` patterns for validation and auth failures.
- Existing auth/session lifecycle from stories 1.2-1.3 should be treated as authoritative identity source for create-task operations.

### Project Structure Notes

- Expected backend touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/`
  - `task-tracker-api/TaskTracker.Api/Features/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Entities/`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/TaskTrackerDbContext.cs`
  - `task-tracker-api/TaskTracker.Api/Infrastructure/Persistence/Migrations/`
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/`

- Expected frontend touch points:
  - `task-tracker-web/src/app/features/`
  - `task-tracker-web/src/app/shared/services/`
  - `task-tracker-web/src/app/shared/models/`

### Testing Requirements

- Verify successful create persists task with authenticated user ownership and returns normalized task payload.
- Verify invalid request payload returns deterministic RFC 7807 contract with stable `code` and `traceId`.
- Verify unauthorized create requests return expected auth contract.
- Verify ownership cannot be injected/overridden by client payload.
- Verify frontend service/form mappings align to request/response contracts and handle Problem Details errors.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 2, Story 2.1]
- Functional requirements (`FR8`, `FR13`, `FR27`) and error/security expectations (`NFR7`): [Source: _bmad-output/planning-artifacts/epics.md, Functional Requirements and NonFunctional Requirements]
- API contract and architecture constraints (`/api/v1`, Problem Details, ownership checks): [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions; API and Communication Patterns]
- Product scope expectations for task CRUD baseline: [Source: _bmad-output/planning-artifacts/prd.md, MVP - Minimum Viable Product]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed via local BMAD skill instructions

### Completion Notes List

- Implemented `POST /api/v1/tasks` with authenticated ownership assignment derived from token identity.
- Added task persistence entity, EF Core mappings, and migrations for SQL Server-backed task storage.
- Added create-task validation and RFC 7807 Problem Details responses with stable `code` and `traceId`.
- Added integration tests for success, validation failure, unauthorized access, malformed payload handling, and ownership impersonation regression.
- Added frontend create-task service, form model, component flow, and unit tests aligned to API contracts.

### File List

- _bmad-output/implementation-artifacts/2-1-create-task-domain-and-api-contracts.md
