import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CreateTaskRequest, UpdateTaskRequest } from '../models/task.models';
import { TaskService } from './task.service';

describe('TaskService', () => {
  let service: TaskService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        TaskService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(TaskService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('posts create-task payload and returns normalized task response', () => {
    const payload: CreateTaskRequest = {
      title: 'Plan sprint backlog',
      description: 'Draft story priorities for next sprint',
      dueAtUtc: '2026-04-27T18:00:00Z',
      priority: 'medium',
      category: 'planning'
    };

    let responseBody: unknown;
    service.createTask(payload).subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/tasks');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(payload);

    request.flush({
      id: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
      title: 'Plan sprint backlog',
      description: 'Draft story priorities for next sprint',
      dueAtUtc: '2026-04-27T18:00:00Z',
      priority: 'medium',
      category: 'planning',
      isCompleted: false,
      createdAtUtc: '2026-04-25T11:30:12Z',
      updatedAtUtc: '2026-04-25T11:30:12Z'
    });

    expect(responseBody).toBeTruthy();
  });

  it('maps API errors to Problem Details shape with code and traceId', () => {
    const payload: CreateTaskRequest = {
      title: '',
      priority: 'medium',
      category: 'planning'
    };

    let receivedError: unknown;
    service.createTask(payload).subscribe({
      next: () => fail('Expected validation error'),
      error: (error) => {
        receivedError = error;
      }
    });

    const request = httpMock.expectOne('/api/v1/tasks');
    request.flush(
      {
        type: 'https://api.tasktracker.local/problems/validation',
        title: 'Validation failed',
        status: 400,
        code: 'validation.request.invalid',
        traceId: '0HN1FDHJ123',
        errors: {
          title: ['The title field is required.']
        }
      },
      { status: 400, statusText: 'Bad Request' }
    );

    const problem = receivedError as { code: string; traceId: string; errors: Record<string, string[]> };
    expect(problem.code).toBe('validation.request.invalid');
    expect(problem.traceId).toBe('0HN1FDHJ123');
    expect(problem.errors['title'][0]).toContain('required');
  });

  it('requests all tasks without a state query parameter by default', () => {
    let responseBody: unknown;
    service.getTasks().subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/tasks');
    expect(request.request.method).toBe('GET');

    request.flush({
      items: [],
      summary: {
        activeCount: 0,
        completedCount: 0
      }
    });

    expect(responseBody).toBeTruthy();
  });

  it('requests filtered tasks with state query parameter', () => {
    let responseBody: unknown;
    service.getTasks('completed').subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/tasks?state=completed');
    expect(request.request.method).toBe('GET');

    request.flush({
      items: [
        {
          id: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
          title: 'Completed task',
          description: 'done',
          dueAtUtc: null,
          priority: 'low',
          category: 'ops',
          isCompleted: true,
          createdAtUtc: '2026-04-25T11:30:12Z',
          updatedAtUtc: '2026-04-25T11:30:12Z'
        }
      ],
      summary: {
        activeCount: 1,
        completedCount: 1
      }
    });

    expect(responseBody).toBeTruthy();
  });

  it('puts update-task payload to task resource endpoint', () => {
    const payload: UpdateTaskRequest = {
      title: 'Plan sprint backlog v2',
      description: 'Finalize priorities after stakeholder sync',
      dueAtUtc: '2026-04-28T17:00:00Z',
      priority: 'high',
      category: 'planning'
    };

    let responseBody: unknown;
    service.updateTask('7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12', payload).subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/tasks/7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual(payload);

    request.flush({
      id: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
      title: 'Plan sprint backlog v2',
      description: 'Finalize priorities after stakeholder sync',
      dueAtUtc: '2026-04-28T17:00:00Z',
      priority: 'high',
      category: 'planning',
      isCompleted: false,
      createdAtUtc: '2026-04-25T11:30:12Z',
      updatedAtUtc: '2026-04-26T09:15:03Z'
    });

    expect(responseBody).toBeTruthy();
  });
});