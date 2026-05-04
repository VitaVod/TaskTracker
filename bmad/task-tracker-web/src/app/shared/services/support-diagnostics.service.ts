import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  PrivilegedAuditResponse,
  SupportDiagnosticsProblemDetails,
  SupportTimelineEventType,
  SupportTimelineResponse,
  SupportUserDiagnosticsResponse
} from '../models/support-diagnostics.models';

export interface SupportTimelineQueryOptions {
  eventType: SupportTimelineEventType | null;
  startUtc: string;
  endUtc: string;
  page: number;
  maxItems: number;
}

export interface PrivilegedAuditQueryOptions {
  actorUserId: string | null;
  targetUserId: string | null;
  actionType: string | null;
  startUtc: string;
  endUtc: string;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class SupportDiagnosticsService {
  private readonly httpClient = inject(HttpClient);
  private readonly endpoint = '/api/v1/ops/support/users';
  private readonly privilegedAuditEndpoint = '/api/v1/ops/admin-support/privileged-audits';

  getUserDiagnostics(userId: string, windowDays = 14, markerLimit = 25): Observable<SupportUserDiagnosticsResponse> {
    const encodedUserId = encodeURIComponent(userId.trim());
    return this.httpClient
      .get<SupportUserDiagnosticsResponse>(
        `${this.endpoint}/${encodedUserId}?windowDays=${windowDays}&markerLimit=${markerLimit}`
      )
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  getUserTimeline(userId: string, options: SupportTimelineQueryOptions): Observable<SupportTimelineResponse> {
    const encodedUserId = encodeURIComponent(userId.trim());
    const params = new URLSearchParams();

    if (options.eventType) {
      params.set('eventType', options.eventType);
    }

    params.set('startUtc', options.startUtc);
    params.set('endUtc', options.endUtc);
    params.set('page', options.page.toString());
    params.set('maxItems', options.maxItems.toString());

    return this.httpClient
      .get<SupportTimelineResponse>(`${this.endpoint}/${encodedUserId}/timeline?${params.toString()}`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  getPrivilegedAudits(options: PrivilegedAuditQueryOptions): Observable<PrivilegedAuditResponse> {
    const params = new URLSearchParams();

    if (options.actorUserId) {
      params.set('actorUserId', options.actorUserId);
    }

    if (options.targetUserId) {
      params.set('targetUserId', options.targetUserId);
    }

    if (options.actionType) {
      params.set('actionType', options.actionType);
    }

    params.set('startUtc', options.startUtc);
    params.set('endUtc', options.endUtc);
    params.set('page', options.page.toString());
    params.set('pageSize', options.pageSize.toString());

    return this.httpClient
      .get<PrivilegedAuditResponse>(`${this.privilegedAuditEndpoint}?${params.toString()}`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  private normalizeProblemDetails(error: HttpErrorResponse): SupportDiagnosticsProblemDetails {
    const payload = (error.error ?? {}) as SupportDiagnosticsProblemDetails;

    return {
      type: payload.type,
      title: payload.title ?? 'Request failed',
      status: payload.status ?? error.status,
      code: payload.code ?? 'ops.support_diagnostics.request.failed',
      traceId: payload.traceId,
      detail: payload.detail,
      errors: payload.errors
    };
  }
}
