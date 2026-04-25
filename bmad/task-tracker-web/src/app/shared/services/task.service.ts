import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  CreateTaskRequest,
  TaskListResponse,
  TaskListState,
  TaskProblemDetails,
  TaskResponse,
  UpdateTaskRequest
} from '../models/task.models';

@Injectable({ providedIn: 'root' })
export class TaskService {
  private readonly httpClient = inject(HttpClient);
  private readonly endpoint = '/api/v1/tasks';

  createTask(payload: CreateTaskRequest): Observable<TaskResponse> {
    return this.httpClient
      .post<TaskResponse>(this.endpoint, payload)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  getTasks(state: TaskListState = 'all'): Observable<TaskListResponse> {
    const query = state === 'all' ? '' : `?state=${state}`;
    return this.httpClient
      .get<TaskListResponse>(`${this.endpoint}${query}`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  updateTask(taskId: string, payload: UpdateTaskRequest): Observable<TaskResponse> {
    return this.httpClient
      .put<TaskResponse>(`${this.endpoint}/${taskId}`, payload)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  private normalizeProblemDetails(error: HttpErrorResponse): TaskProblemDetails {
    const payload = (error.error ?? {}) as TaskProblemDetails;
    return {
      type: payload.type,
      title: payload.title ?? 'Request failed',
      status: payload.status ?? error.status,
      code: payload.code ?? 'task.request.failed',
      traceId: payload.traceId,
      detail: payload.detail,
      errors: payload.errors
    };
  }
}