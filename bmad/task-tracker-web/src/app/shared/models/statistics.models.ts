import { TaskProblemDetails } from './task.models';

export interface GlobalStatisticsSnapshot {
  totalTasksCreated: number;
  totalTasksCompleted: number;
}

export type StatisticsProblemDetails = TaskProblemDetails;
