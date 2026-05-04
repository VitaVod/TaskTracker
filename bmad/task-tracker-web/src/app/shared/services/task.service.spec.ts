import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CreateTaskRequest, ToggleTaskCompletionRequest, UpdateTaskRequest } from '../models/task.models';
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
      category: 'work'
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
      category: 'work',
      difficulty: 'easy',
      energyLevel: 'medium',
      contextTag: null,
      effortPoints: null,
      predictedDurationMinutes: null,
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
      category: 'work'
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
          category: 'personal',
          difficulty: 'easy',
          energyLevel: 'low',
          contextTag: 'home',
          effortPoints: 3,
          predictedDurationMinutes: 45,
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
      category: 'work',
      difficulty: 'medium',
      energyLevel: 'high',
      contextTag: 'office',
      effortPoints: 8
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
      category: 'work',
      difficulty: 'medium',
      energyLevel: 'high',
      contextTag: 'office',
      effortPoints: 8,
      predictedDurationMinutes: 90,
      isCompleted: false,
      createdAtUtc: '2026-04-25T11:30:12Z',
      updatedAtUtc: '2026-04-26T09:15:03Z'
    });

    expect(responseBody).toBeTruthy();
  });

  it('requests tasks with combined state and planning filters', () => {
    let responseBody: unknown;
    service.getTasks('active', { energyLevel: 'high', contextTag: 'Office' }).subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/tasks?state=active&energyLevel=high&contextTag=office');
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

  it('requests tasks with title and priority planning filters', () => {
    let responseBody: unknown;
    service.getTasks('active', { title: 'Sprint', priority: 'high' }).subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/tasks?state=active&title=Sprint&priority=high');
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

  it('patches completion payload with idempotency header', () => {
    const payload: ToggleTaskCompletionRequest = {
      isCompleted: true
    };

    let responseBody: unknown;
    service.toggleTaskCompletion('7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12', payload, 'idempotency-key-123').subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/tasks/7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12/completion');
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual(payload);
    expect(request.request.headers.get('Idempotency-Key')).toBe('idempotency-key-123');

    request.flush({
      task: {
        id: '7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12',
        title: 'Plan sprint backlog v2',
        description: 'Finalize priorities after stakeholder sync',
        dueAtUtc: '2026-04-28T17:00:00Z',
        priority: 'high',
        category: 'work',
        difficulty: 'medium',
        energyLevel: 'high',
        contextTag: 'office',
        effortPoints: 8,
        predictedDurationMinutes: 90,
        isCompleted: true,
        createdAtUtc: '2026-04-25T11:30:12Z',
        updatedAtUtc: '2026-04-26T09:15:03Z'
      },
      progression: {
        completionEventId: 'event-1',
        xpLedgerEntryId: 'ledger-1',
        xpGranted: 20,
        eligibleForXp: true,
        idempotentReplay: false,
        idempotencyKey: 'idempotency-key-123',
        traceId: 'trace-1'
      }
    });

    expect(responseBody).toBeTruthy();
  });

  it('deletes task resource by id', () => {
    let completed = false;
    service.deleteTask('7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12').subscribe(() => {
      completed = true;
    });

    const request = httpMock.expectOne('/api/v1/tasks/7f8d3d3f-1bba-4b43-8de6-2bf5f83e8a12');
    expect(request.request.method).toBe('DELETE');

    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(completed).toBeTrue();
  });
});