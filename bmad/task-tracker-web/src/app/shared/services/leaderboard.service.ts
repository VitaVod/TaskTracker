import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  LeaderboardProblemDetails,
  LeaderboardResponse,
  LeaderboardType
} from '../models/leaderboard.models';

@Injectable({ providedIn: 'root' })
export class LeaderboardService {
  private readonly httpClient = inject(HttpClient);
  private readonly endpoint = '/api/v1/leaderboards';

  getLeaderboard(type: LeaderboardType, page = 1, pageSize = 20): Observable<LeaderboardResponse> {
    const query = `type=${type}&page=${page}&pageSize=${pageSize}`;

    return this.httpClient
      .get<LeaderboardResponse>(`${this.endpoint}?${query}`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  private normalizeProblemDetails(error: HttpErrorResponse): LeaderboardProblemDetails {
    const payload = (error.error ?? {}) as LeaderboardProblemDetails;
    return {
      type: payload.type,
      title: payload.title ?? 'Request failed',
      status: payload.status ?? error.status,
      code: payload.code ?? 'leaderboard.request.failed',
      traceId: payload.traceId,
      detail: payload.detail,
      errors: payload.errors
    };
  }
}
