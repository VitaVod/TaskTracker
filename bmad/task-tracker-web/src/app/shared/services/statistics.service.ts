import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { GlobalStatisticsSnapshot, StatisticsProblemDetails } from '../models/statistics.models';

@Injectable({ providedIn: 'root' })
export class StatisticsService {
  private readonly httpClient = inject(HttpClient);
  private readonly endpoint = '/api/v1/statistics';

  getGlobalStatistics(): Observable<GlobalStatisticsSnapshot> {
    return this.httpClient
      .get<GlobalStatisticsSnapshot>(`${this.endpoint}/global`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  private normalizeProblemDetails(error: HttpErrorResponse): StatisticsProblemDetails {
    const payload = (error.error ?? {}) as StatisticsProblemDetails;
    return {
      type: payload.type,
      title: payload.title ?? 'Request failed',
      status: payload.status ?? error.status,
      code: payload.code ?? 'statistics.request.failed',
      traceId: payload.traceId,
      detail: payload.detail,
      errors: payload.errors
    };
  }
}
