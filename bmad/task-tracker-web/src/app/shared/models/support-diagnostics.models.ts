export interface SupportDiagnosticWindow {
  windowDays: number;
  windowStartUtc: string;
  markerLimit: number;
}

export interface SupportRecentCompletion {
  taskId: string;
  title: string;
  completedAtUtc: string;
}

export interface SupportAccountSnapshot {
  userId: string;
  email: string;
  displayName: string;
  role: string;
  timeZoneId: string;
  locale: string;
  leaderboardParticipationMode: 'public' | 'anonymous' | 'hidden';
  isSuspiciousFlagged: boolean;
  createdAtUtc: string;
  modifiedAtUtc: string;
}

export interface SupportTaskStateSnapshot {
  totalCount: number;
  completedCount: number;
  activeCount: number;
  lastCompletedAtUtc: string | null;
  recentCompletions: SupportRecentCompletion[];
}

export interface SupportXpStateSnapshot {
  totalXp: number;
  ledgerEntryCount: number;
  lastGrantedAtUtc: string | null;
  outcomeReasonCode: string;
  outcomeExplanation: string;
}

export interface SupportStreakStateSnapshot {
  outcome: string;
  currentStreakDays: number;
  longestStreakDays: number;
  timeZoneId: string;
  evaluationWindowStartUtc: string;
  evaluationWindowEndUtc: string;
  lastEvaluatedAtUtc: string;
  outcomeReasonCode: string;
  outcomeExplanation: string;
  isRecoveryPromptVisible: boolean;
  recoveryReason: string | null;
  recommendedAction: string | null;
  recoveryExplanation: string | null;
}

export interface SupportProgressMarker {
  markerType: string;
  markerId: string;
  occurredAtUtc: string;
  summary: string;
  traceId: string | null;
  correlationRef: string | null;
}

export interface SupportUserDiagnosticsResponse {
  account: SupportAccountSnapshot;
  taskState: SupportTaskStateSnapshot;
  xpState: SupportXpStateSnapshot;
  streakState: SupportStreakStateSnapshot;
  window: SupportDiagnosticWindow;
  recentMarkers: SupportProgressMarker[];
  correlationId: string;
  traceId: string;
}

export type SupportTimelineEventType = 'taskCompletion' | 'xpLedger' | 'moderation' | 'streakEvaluation';

export interface SupportTimelineFilters {
  eventType: SupportTimelineEventType | null;
  startUtc: string;
  endUtc: string;
}

export interface SupportTimelineEvent {
  eventId: string;
  eventType: SupportTimelineEventType;
  occurredAtUtc: string;
  sourceSubsystem: string;
  messageCode: string;
  message: string;
  ruleOutcome: string;
  traceId: string | null;
  correlationId: string | null;
  actorContext: string;
  targetContext: string;
  relatedEntityId: string | null;
}

export interface SupportTimelineResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  hasNextPage: boolean;
  filters: SupportTimelineFilters;
  items: SupportTimelineEvent[];
  correlationId: string;
  traceId: string;
}

export interface PrivilegedAuditFilters {
  actorUserId: string | null;
  targetUserId: string | null;
  actionType: string | null;
  startUtc: string;
  endUtc: string;
}

export interface PrivilegedAuditItem {
  auditId: string;
  actorUserId: string;
  actorRole: string;
  targetUserId: string | null;
  actionType: string;
  reasonCode: string;
  reasonText: string;
  outcome: string;
  occurredAtUtc: string;
  correlationId: string;
  traceId: string;
}

export interface PrivilegedAuditResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  hasNextPage: boolean;
  filters: PrivilegedAuditFilters;
  items: PrivilegedAuditItem[];
  correlationId: string;
  traceId: string;
}

export interface SupportDiagnosticsProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  code?: string;
  traceId?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}
