export type LeaderboardType = 'streak' | 'completedTasks';
export type LeaderboardIdentityMode = 'public' | 'anonymous';

export interface LeaderboardEntry {
  rank: number;
  publicIdentity: string;
  identityMode: LeaderboardIdentityMode;
  avatarMarker: string;
  metricValue: number;
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
