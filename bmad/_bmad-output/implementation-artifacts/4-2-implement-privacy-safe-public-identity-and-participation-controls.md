# Story 4.2: Implement Privacy-Safe Public Identity and Participation Controls

Status: done

<!-- Note: Validation is optional. Run validate-create-story for quality check before dev-story. -->

## Story

As a user,
I want leaderboard visibility aligned with privacy policy,
so that I can participate comfortably.

## Acceptance Criteria

1. Given leaderboard participant profiles, when leaderboard payload is generated, then only approved public identity fields are exposed.
2. Given leaderboard participant profiles, when leaderboard payload is generated, then participation respects privacy settings and policy rules.

## Tasks / Subtasks

- [x] Define privacy-safe leaderboard identity contract (AC: 1)
  - [x] Define and document allowed public identity fields for leaderboard rows (for example display name alias and non-sensitive avatar marker) and explicitly exclude private profile attributes.
  - [x] Define explicit participation identity modes aligned with product privacy rules (public alias, privacy-safe anonymous alias, private/hidden) and map each mode to deterministic API payload behavior.
  - [x] Add deterministic fallback identity behavior for users without public identity configured (for example stable anonymous label).
  - [x] Update leaderboard response contracts to reflect privacy-safe identity semantics without breaking pagination/rank metadata introduced in Story 4.1.

- [x] Implement participation controls in leaderboard read path (AC: 2)
  - [x] Add or reuse a per-user participation/privacy setting that governs inclusion in public leaderboard results.
  - [x] Enforce policy in leaderboard query/repository layer so opted-out users are excluded or transformed according to policy before ranking payload serialization.
  - [x] Keep deterministic ordering semantics intact when filtering participants (metric ordering and tie-break stability must remain reproducible).
  - [x] Trigger leaderboard read-cache invalidation (or equivalent freshness mechanism) when participation mode or public-identity settings change so privacy updates are reflected immediately in shared views.

- [x] Expose and validate participation setting management (AC: 2)
  - [x] Reuse existing account/profile settings endpoints from Epic 1 where possible; avoid creating duplicate preference APIs.
  - [x] Enforce and document deterministic default participation policy for new and existing users according to privacy rules (no implicit exposure on migration/backfill).
  - [x] Add validation and authorization checks so users can only modify their own participation setting.
  - [x] Ensure API errors map to existing Problem Details conventions.

- [x] Integrate privacy-safe identity rendering in leaderboard UI contracts (AC: 1, 2)
  - [x] Update shared leaderboard models/service mapping to consume new public identity fields.
  - [x] Ensure opted-out participants are not shown as personally identifiable rows in leaderboard UI.
  - [x] Preserve accessible labeling for anonymous/public identity rows.

- [x] Add tests for privacy filtering and participation policy behavior (AC: 1, 2)
  - [x] Integration tests for leaderboard payload field allowlist and private-field exclusion.
  - [x] Integration tests for opt-in/opt-out behavior and authenticated ownership checks on participation preference changes.
  - [x] Regression tests confirming deterministic rank ordering and pagination metadata remain stable after privacy filtering.

## Dev Notes

- Story 4.2 extends Story 4.1 leaderboard read models by adding policy-safe identity projection and participation enforcement.
- Reuse profile/account preferences patterns from Story 1.4 rather than introducing parallel preference storage.
- Privacy enforcement must happen server-side before payload serialization; frontend should not be responsible for hiding sensitive fields.
- Privacy preference changes must produce fast read consistency in leaderboard responses (cache invalidation/freshness policy) to prevent stale identity exposure.
- Keep `/api/v1` contract consistency, existing authorization policy style, and Problem Details error shape.

### Project Structure Notes

- Backend expected touch points:
  - `task-tracker-api/TaskTracker.Api/Controllers/LeaderboardsController.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Contracts/LeaderboardContracts.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/ILeaderboardRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Leaderboards/Repositories/LeaderboardRepository.cs`
  - `task-tracker-api/TaskTracker.Api/Features/Profile` (or existing account settings feature used in Story 1.4)
  - `task-tracker-api/tests/TaskTracker.Api.Tests/Integration/LeaderboardsControllerTests.cs`

- Frontend expected touch points:
  - `task-tracker-web/src/app/shared/services/leaderboard.service.ts`
  - `task-tracker-web/src/app/shared/models/leaderboard.models.ts`
  - `task-tracker-web/src/app/features` (leaderboard-facing components for identity display behavior)

### Testing Requirements

- Verify leaderboard responses expose only approved public identity fields.
- Verify non-approved/private profile fields are never present in leaderboard payloads.
- Verify participation opt-out removes or policy-transforms users in leaderboard results.
- Verify users can only manage their own participation preference.
- Verify deterministic rank order and pagination metadata remain stable after privacy filters are applied.
- Verify privacy preference changes are reflected in leaderboard responses within defined freshness bounds and do not leak stale identity data.
- Verify anonymous/fallback identity rendering remains accessible in UI.

### Previous Story Intelligence

- Story 4.1 already implemented deterministic ordering, pagination boundaries, and leaderboard API contracts; Story 4.2 should extend these contracts rather than redesigning them.
- Story 4.1 intentionally deferred privacy/identity policy behavior to this story; avoid leaking identity fields through intermediate DTOs.

### Git Intelligence Summary

- Recent repository history shows a consistent pattern of deterministic business rules and integration-test-heavy delivery.
- Story 4.2 should maintain that pattern: server-authoritative privacy enforcement with explicit integration coverage.

### References

- Story definition and acceptance criteria: [Source: _bmad-output/planning-artifacts/epics.md, Epic 4, Story 4.2]
- Leaderboard/privacy requirements context (`FR21`, `FR22`, `FR23`, `FR28`, `NFR5`, `NFR7`, `NFR14`, `NFR17`): [Source: _bmad-output/planning-artifacts/epics.md, Requirements Inventory]
- Product trust and comfort goals for social comparison: [Source: _bmad-output/planning-artifacts/prd.md, Functional Requirements; Success Criteria]
- Architecture constraints for privacy-safe identity exposure and shared read model behavior: [Source: _bmad-output/planning-artifacts/architecture.md, Core Architectural Decisions]
- UX guidance for motivational, non-shaming leaderboard surfaces and accessible row semantics: [Source: _bmad-output/planning-artifacts/ux-design-specification.md, UX Design Requirements; Accessibility Considerations]
- Prior implementation baseline: [Source: _bmad-output/implementation-artifacts/4-1-implement-streak-and-completed-task-leaderboard-read-models.md]

## Dev Agent Record

### Agent Model Used

GPT-5.3-Codex

### Debug Log References

- create-story workflow executed manually (BMAD CLI unavailable in current shell)
- validate-create-story 4.2 executed manually (workflow checklist-driven validation)

### Completion Notes List

- Created Story 4.2 implementation artifact with acceptance criteria, task breakdown, and architecture/testing guardrails.
- Advanced sprint tracking state for Story 4.2 from `backlog` to `ready-for-dev`.
- Ran validate-create-story review and hardened guidance for participation modes, default privacy policy, and cache-freshness privacy safety.

### File List

- _bmad-output/implementation-artifacts/4-2-implement-privacy-safe-public-identity-and-participation-controls.md
- _bmad-output/implementation-artifacts/4-2-implement-privacy-safe-public-identity-and-participation-controls-validation-report.md
- _bmad-output/implementation-artifacts/sprint-status.yaml