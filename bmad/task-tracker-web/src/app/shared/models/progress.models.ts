import { TaskProblemDetails, TaskStreakOutcome } from './task.models';

export type ProgressTrendGranularity = 'daily' | 'weekly';

export interface ProgressExplanation {
  reasonCode: string;
  message: string;
}

export interface ProgressXpSummary {
  totalXp: number;
  ledgerEntryCount: number;
  lastGrantedAtUtc: string | null;
  levelProgress: ProgressLevelSnapshot;
  outcomeExplanation: ProgressExplanation;
}

export interface ProgressLevelSnapshot {
  currentLevel: number;
  currentLevelThresholdXp: number;
  nextLevel: number;
  nextLevelThresholdXp: number;
  percentToNextLevel: number;
  bandMilestoneLevels: number[];
  reachedBandCount: number;
  nextBandLevel: number | null;
}

export interface ProgressStreakSnapshot {
  outcome: TaskStreakOutcome;
  currentStreakDays: number;
  longestStreakDays: number;
  timeZoneId: string;
  evaluationWindowStartUtc: string;
  evaluationWindowEndUtc: string;
  lastEvaluatedAtUtc: string;
  isRecoveryPromptVisible: boolean;
  recoveryReason: string | null;
  recommendedAction: string | null;
  outcomeExplanation: ProgressExplanation;
  recoveryExplanation: ProgressExplanation | null;
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
