import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  ProgressProblemDetails,
  ProgressStreakSnapshot,
  ProgressTrendGranularity,
  ProgressTrendSummary,
  ProgressXpSummary
} from '../models/progress.models';

@Injectable({ providedIn: 'root' })
export class ProgressService {
  private readonly httpClient = inject(HttpClient);
  private readonly endpoint = '/api/v1/progress';

  getXpSummary(): Observable<ProgressXpSummary> {
    return this.httpClient
      .get<ProgressXpSummary>(`${this.endpoint}/xp-summary`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  getStreakSnapshot(): Observable<ProgressStreakSnapshot> {
    return this.httpClient
      .get<ProgressStreakSnapshot>(`${this.endpoint}/streak`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  getTrendSummary(granularity: ProgressTrendGranularity = 'daily', windowDays = 30): Observable<ProgressTrendSummary> {
    return this.httpClient
      .get<ProgressTrendSummary>(`${this.endpoint}/trend?granularity=${granularity}&windowDays=${windowDays}`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  private normalizeProblemDetails(error: HttpErrorResponse): ProgressProblemDetails {
    const payload = (error.error ?? {}) as ProgressProblemDetails;
    return {
      type: payload.type,
      title: payload.title ?? 'Request failed',
      status: payload.status ?? error.status,
      code: payload.code ?? 'progress.request.failed',
      traceId: payload.traceId,
      detail: payload.detail,
      errors: payload.errors
    };
  }
}
