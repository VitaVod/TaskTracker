export type TaskPriority = 'low' | 'medium' | 'high';
export type TaskListState = 'active' | 'completed' | 'all';
export const TASK_CATEGORIES = [
  'work',
  'learning',
  'personal',
  'health',
  'finance',
  'household',
  'social',
  'travel',
  'admin',
  'other'
] as const;
export type TaskCategory = (typeof TASK_CATEGORIES)[number];

export const TASK_CATEGORY_LABELS: Record<TaskCategory, string> = {
  work: 'Work',
  learning: 'Learning',
  personal: 'Personal',
  health: 'Health',
  finance: 'Finance',
  household: 'Home',
  social: 'Social',
  travel: 'Travel',
  admin: 'Admin',
  other: 'Other'
};

export const TASK_CATEGORY_OPTIONS: ReadonlyArray<{ value: TaskCategory; label: string }> = TASK_CATEGORIES.map((value) => ({
  value,
  label: TASK_CATEGORY_LABELS[value]
}));

export function isTaskCategory(value: string): value is TaskCategory {
  return (TASK_CATEGORIES as readonly string[]).includes(value);
}

export function toTaskCategoryLabel(category: string): string {
  if (isTaskCategory(category)) {
    return TASK_CATEGORY_LABELS[category];
  }

  if (category.trim().length === 0) {
    return 'Other';
  }

  return category.charAt(0).toUpperCase() + category.slice(1);
}

export interface CreateTaskRequest {
  title: string;
  description?: string;
  dueAtUtc?: string | null;
  priority: TaskPriority;
  category: TaskCategory;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string;
  dueAtUtc?: string | null;
  priority: TaskPriority;
  category: TaskCategory;
}

export interface ToggleTaskCompletionRequest {
  isCompleted: boolean;
}

export interface TaskResponse {
  id: string;
  title: string;
  description: string;
  dueAtUtc: string | null;
  priority: TaskPriority;
  category: string;
  isCompleted: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TaskListSummary {
  activeCount: number;
  completedCount: number;
}

export interface TaskListResponse {
  items: TaskResponse[];
  summary: TaskListSummary;
}

export type TaskUiState =
  | { kind: 'loading' }
  | { kind: 'empty'; filter: TaskListState }
  | { kind: 'ready'; tasks: TaskResponse[] }
  | {
    kind: 'error';
    scope: 'load' | 'mutation';
    message: string;
    code?: string;
    traceId?: string;
  };

export interface TaskProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  code?: string;
  traceId?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}