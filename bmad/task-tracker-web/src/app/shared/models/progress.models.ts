import { TaskProblemDetails, TaskStreakOutcome } from './task.models';

export type ProgressTrendGranularity = 'daily' | 'weekly';

export interface ProgressXpSummary {
  totalXp: number;
  ledgerEntryCount: number;
  lastGrantedAtUtc: string | null;
}

export interface ProgressStreakSnapshot {
  outcome: TaskStreakOutcome;
  currentStreakDays: number;
  longestStreakDays: number;
  timeZoneId: string;
  evaluationWindowStartUtc: string;
  evaluationWindowEndUtc: string;
  lastEvaluatedAtUtc: string;
}

export interface ProgressTrendPoint {
  bucketStartUtc: string;
  bucketEndUtc: string;
  completedTaskCount: number;
  xpGranted: number;
}

export interface ProgressTrendSummary {
  granularity: ProgressTrendGranularity;
  windowDays: number;
  timeZoneId: string;
  rangeStartUtc: string;
  rangeEndUtc: string;
  items: ProgressTrendPoint[];
}

export type ProgressProblemDetails = TaskProblemDetails;
