import { TaskCategory, TaskPriority } from '../../shared/models/task.models';

export interface CreateTaskFormValue {
  title: string;
  description: string;
  dueAtUtc: string | null;
  priority: TaskPriority;
  category: TaskCategory;
}