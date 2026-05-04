export type SuspiciousAnomalyType = 'activitySpike' | 'rankingMismatch';

export interface SuspiciousCaseItem {
  caseId: string;
  publicIdentity: string;
  identityMode: 'public' | 'anonymous';
  anomalyType: SuspiciousAnomalyType;
  signalSummary: string;
  severity: number;
  detectedAtUtc: string;
  lastActivityAtUtc: string | null;
  correlationRef: string;
  destructiveConfirmationToken: string | null;
}

export type ModerationActionType = 'rankingCorrection' | 'flagEntity';

export interface ModerationActionRequest {
  actionType: ModerationActionType;
  reasonCode: string;
  reasonText: string;
  confirmDestructive: boolean;
  confirmationToken: string | null;
}

export interface ModerationActionResponse {
  auditId: string;
  caseId: string;
  actionType: ModerationActionType;
  outcome: 'succeeded' | 'alreadyApplied';
  correlationRef: string;
  processedAtUtc: string;
  traceId: string;
}

export interface SuspiciousCasesResponse {
  page: number;
  pageSize: number;
  totalCount: number;
  hasNextPage: boolean;
  items: SuspiciousCaseItem[];
}

export interface SuspiciousCasesProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  code?: string;
  traceId?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}
