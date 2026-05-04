import { TaskCategory, TaskDifficulty, TaskEnergyLevel, TaskPriority } from '../../shared/models/task.models';

export interface CreateTaskFormValue {
  title: string;
  description: string;
  dueAtUtc: string | null;
  priority: TaskPriority;
  category: TaskCategory;
  difficulty: TaskDifficulty;
  energyLevel: TaskEnergyLevel;
  contextTag: string;
  effortPoints: number | null;
}