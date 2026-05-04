import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  ModerationActionRequest,
  ModerationActionResponse,
  SuspiciousAnomalyType,
  SuspiciousCasesProblemDetails,
  SuspiciousCasesResponse
} from '../models/suspicious-cases.models';

@Injectable({ providedIn: 'root' })
export class SuspiciousCasesService {
  private readonly httpClient = inject(HttpClient);
  private readonly endpoint = '/api/v1/ops/admin/suspicious-cases';

  getCases(
    anomalyType: SuspiciousAnomalyType | 'all',
    page = 1,
    pageSize = 20
  ): Observable<SuspiciousCasesResponse> {
    const queryParts = [`page=${page}`, `pageSize=${pageSize}`];

    if (anomalyType !== 'all') {
      queryParts.push(`anomalyType=${anomalyType}`);
    }

    return this.httpClient
      .get<SuspiciousCasesResponse>(`${this.endpoint}?${queryParts.join('&')}`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  applyModerationAction(caseId: string, payload: ModerationActionRequest): Observable<ModerationActionResponse> {
    return this.httpClient
      .post<ModerationActionResponse>(`${this.endpoint}/${encodeURIComponent(caseId)}/moderation-actions`, payload)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  private normalizeProblemDetails(error: HttpErrorResponse): SuspiciousCasesProblemDetails {
    const payload = (error.error ?? {}) as SuspiciousCasesProblemDetails;

    return {
      type: payload.type,
      title: payload.title ?? 'Request failed',
      status: payload.status ?? error.status,
      code: payload.code ?? 'ops.suspicious_cases.request.failed',
      traceId: payload.traceId,
      detail: payload.detail,
      errors: payload.errors
    };
  }
}
