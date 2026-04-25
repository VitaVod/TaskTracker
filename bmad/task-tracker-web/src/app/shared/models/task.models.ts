export type TaskPriority = 'low' | 'medium' | 'high';
export type TaskListState = 'active' | 'completed' | 'all';

export interface CreateTaskRequest {
  title: string;
  description?: string;
  dueAtUtc?: string | null;
  priority: TaskPriority;
  category: string;
}

export interface UpdateTaskRequest {
  title: string;
  description?: string;
  dueAtUtc?: string | null;
  priority: TaskPriority;
  category: string;
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

export interface TaskProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  code?: string;
  traceId?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}