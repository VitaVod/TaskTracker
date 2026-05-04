export type LeaderboardType = 'streak' | 'completedTasks';
export type LeaderboardIdentityMode = 'public' | 'anonymous';

export interface LeaderboardEntry {
  rank: number;
  publicIdentity: string;
  identityMode: LeaderboardIdentityMode;
  avatarMarker: string;
  metricValue: number;
  publicProfileHandle: string | null;
}

export interface LeaderboardResponse {
  type: LeaderboardType;
  page: number;
  pageSize: number;
  totalCount: number;
  hasNextPage: boolean;
  items: LeaderboardEntry[];
}

export interface LeaderboardProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export type PublicProfileVisibility = 'public' | 'anonymous';

export interface PublicProfileStatistics {
  currentStreakDays: number;
  longestStreakDays: number;
  completedTaskCount: number;
  totalXp: number;
  lastCompletedAtUtc: string | null;
}

export interface PublicProfileResponse {
  visibility: PublicProfileVisibility;
  publicIdentity: string | null;
  avatarMarker: string | null;
  statistics: PublicProfileStatistics | null;
  message: string | null;
}
