---
title: 'Hide Edit Action For Completed Tasks In Task List'
type: 'bugfix'
created: '2026-05-04T12:59:00Z'
status: 'done'
route: 'one-shot'
---

# Hide Edit Action For Completed Tasks In Task List

## Intent

**Problem:** Completed tasks still exposed edit affordances and stale edit-state paths, allowing users to modify items intended to be locked after completion.

**Approach:** Hide edit actions for completed tasks across all list variants and enforce component-level guards so completed tasks cannot enter or remain in edit mode.

## Suggested Review Order

**UI entry points**

- Hide completed-task edit actions in all rendered task-list branches.
  [`task-list.component.html:208`](../../task-tracker-web/src/app/features/tasks/task-list.component.html#L208)

- Prevent stale edit forms from rendering when task state is completed.
  [`task-list.component.html:222`](../../task-tracker-web/src/app/features/tasks/task-list.component.html#L222)

**Behavioral enforcement**

- Block edit start/toggle immediately when task is completed.
  [`task-list.component.ts:374`](../../task-tracker-web/src/app/features/tasks/task-list.component.ts#L374)

- Reject submit path when edited task is completed or missing.
  [`task-list.component.ts:434`](../../task-tracker-web/src/app/features/tasks/task-list.component.ts#L434)

- Clear edit mode during completion transition and reload reconciliation.
  [`task-list.component.ts:737`](../../task-tracker-web/src/app/features/tasks/task-list.component.ts#L737)

**Regression coverage**

- Verify completed filter hides edit action buttons.
  [`task-list.component.spec.ts:488`](../../task-tracker-web/src/app/features/tasks/task-list.component.spec.ts#L488)

- Cover stale edit id and completed-submit blocking paths.
  [`task-list.component.spec.ts:505`](../../task-tracker-web/src/app/features/tasks/task-list.component.spec.ts#L505)
