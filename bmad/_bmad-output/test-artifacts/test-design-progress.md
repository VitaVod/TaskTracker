---
workflowStatus: 'completed'
totalSteps: 5
stepsCompleted:
  - 'step-01-detect-mode'
  - 'step-02-load-context'
  - 'step-03-risk-and-testability'
  - 'step-04-coverage-plan'
  - 'step-05-generate-output'
lastStep: 'step-05-generate-output'
nextStep: ''
lastSaved: '2026-05-04'
workflowType: 'testarch-test-design'
mode: 'system-level'
inputDocuments:
  - _bmad/tea/config.yaml
  - _bmad-output/planning-artifacts/prd.md
  - _bmad-output/planning-artifacts/architecture.md
  - _bmad-output/implementation-artifacts/sprint-status.yaml
  - .github/skills/bmad-testarch-test-design/resources/knowledge/adr-quality-readiness-checklist.md
  - .github/skills/bmad-testarch-test-design/resources/knowledge/risk-governance.md
  - .github/skills/bmad-testarch-test-design/resources/knowledge/test-levels-framework.md
  - .github/skills/bmad-testarch-test-design/resources/knowledge/test-quality.md
  - .github/skills/bmad-testarch-test-design/resources/knowledge/overview.md
  - .github/skills/bmad-testarch-test-design/resources/knowledge/api-request.md
  - task-tracker-web/package.json
  - task-tracker-api/TaskTracker.Api/TaskTracker.Api.csproj
---

# Test Design Workflow Progress

## Step 1 - Detect Mode and Prerequisites

- Selected mode: System-level.
- Reasoning: User requested a test strategy, and both PRD plus architecture artifacts are available.
- Prerequisites validated:
  - PRD is present and includes FR/NFR sections.
  - Architecture document is present and includes core technical decisions.
  - Sufficient architecture context exists to identify ASRs and pre-implementation blockers.

## Step 2 - Load Context and Knowledge

- Loaded config from TEA module:
  - `tea_use_playwright_utils: true`
  - `tea_use_pactjs_utils: false`
  - `tea_pact_mcp: none`
  - `tea_browser_automation: auto`
  - `test_stack_type: auto`
  - `test_artifacts: {project-root}/_bmad-output/test-artifacts`
- Stack detection result: fullstack.
  - Frontend indicators: Angular 20 workspace and package metadata.
  - Backend indicators: .NET API project and backend test project.
- Loaded planning artifacts:
  - PRD with FR1-FR48 and NFR categories (performance, security, reliability, accessibility, integration).
  - Architecture decisions (modular monolith, SQL Server, JWT auth, cache-read models, idempotency requirements).
- Loaded required knowledge fragments for system-level mode:
  - adr-quality-readiness-checklist
  - risk-governance
  - test-levels-framework
  - test-quality
- Loaded Playwright Utils context due config enablement:
  - overview
  - api-request

## Step 3 - Testability and Risk Assessment

- Performed testability review across controllability, observability, and reliability.
- Identified actionable concerns:
  - Missing explicit test data seeding interface.
  - Missing deterministic clock abstraction for timezone/day-boundary scenarios.
  - Missing cache freshness instrumentation for leaderboard and global stats.
  - Missing explicit correlation standards across audit and support timelines.
- Constructed risk register with scored categories (TECH, SEC, PERF, DATA, OPS).
- High-priority risks (score >=6): R-001, R-002, R-003, R-004, R-005.

## Step 4 - Coverage Plan and Execution Strategy

- Built priority-based coverage plan (P0-P3) with non-duplicated levels:
  - E2E only for critical user journeys.
  - API tests for deterministic domain and authorization invariants.
  - Component tests for key UI feedback and accessibility behavior.
  - Unit tests for rule-edge and algorithmic branches.
- Defined execution strategy using PR / Nightly / Weekly model.
- Generated interval-based QA effort ranges (no false precision).
- Defined quality gates for P0, P1, high-risk mitigations, and overall coverage.

## Step 5 - Output Generation and Validation

Generated artifacts:

- `_bmad-output/test-artifacts/test-design-architecture.md`
- `_bmad-output/test-artifacts/test-design-qa.md`
- `_bmad-output/test-artifacts/test-design/bmad-handoff.md`

Validation summary:

- Required sections for system-level architecture and QA documents are present.
- Risk IDs are consistent across both documents.
- Priority model and execution model are separated (no mixed semantics).
- Estimates are interval-based.
- Outputs are concise and actionable, with architecture concerns separated from QA execution recipes.
