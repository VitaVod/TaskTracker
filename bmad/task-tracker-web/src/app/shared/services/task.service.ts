import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import {
  CreateTaskRequest,
  TaskListFilters,
  TaskListResponse,
  TaskListState,
  ToggleTaskCompletionResponse,
  TaskProblemDetails,
  TaskResponse,
  ToggleTaskCompletionRequest,
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

  getTasks(state: TaskListState = 'all', filters?: TaskListFilters): Observable<TaskListResponse> {
    const queryParts: string[] = [];
    if (state !== 'all') {
      queryParts.push(`state=${encodeURIComponent(state)}`);
    }

    if (filters?.title && filters.title.trim() !== '') {
      queryParts.push(`title=${encodeURIComponent(filters.title.trim())}`);
    }

    if (filters?.priority) {
      queryParts.push(`priority=${encodeURIComponent(filters.priority)}`);
    }

    if (filters?.energyLevel) {
      queryParts.push(`energyLevel=${encodeURIComponent(filters.energyLevel)}`);
    }

    if (filters?.difficulty) {
      queryParts.push(`difficulty=${encodeURIComponent(filters.difficulty)}`);
    }

    if (filters?.contextTag && filters.contextTag.trim() !== '') {
      queryParts.push(`contextTag=${encodeURIComponent(filters.contextTag.trim().toLowerCase())}`);
    }

    const query = queryParts.length > 0 ? `?${queryParts.join('&')}` : '';
    return this.httpClient
      .get<TaskListResponse>(`${this.endpoint}${query}`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  updateTask(taskId: string, payload: UpdateTaskRequest): Observable<TaskResponse> {
    return this.httpClient
      .put<TaskResponse>(`${this.endpoint}/${taskId}`, payload)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  deleteTask(taskId: string): Observable<void> {
    return this.httpClient
      .delete<void>(`${this.endpoint}/${taskId}`)
      .pipe(catchError((error: HttpErrorResponse) => throwError(() => this.normalizeProblemDetails(error))));
  }

  toggleTaskCompletion(taskId: string, payload: ToggleTaskCompletionRequest, idempotencyKey: string): Observable<ToggleTaskCompletionResponse> {
    return this.httpClient
      .patch<ToggleTaskCompletionResponse>(
        `${this.endpoint}/${taskId}/completion`,
        payload,
        {
          headers: new HttpHeaders({
            'Idempotency-Key': idempotencyKey
          })
        })
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