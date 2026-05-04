import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { NotificationPreferencesService } from './notification-preferences.service';

describe('NotificationPreferencesService', () => {
  let service: NotificationPreferencesService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        NotificationPreferencesService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(NotificationPreferencesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('requests notification preferences with deterministic contract shape', () => {
    let responseBody: unknown;

    service.getPreferences().subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/notifications/preferences');
    expect(request.request.method).toBe('GET');

    request.flush({
      reminderEmailEnabled: true,
      reminderCadence: 'daily',
      accountEmailEnabled: true,
      updatedAtUtc: '2026-04-30T08:12:00Z'
    });

    expect(responseBody).toBeTruthy();
  });

  it('patches notification preferences with partial payload', () => {
    let responseBody: unknown;

    service.updatePreferences({
      reminderEmailEnabled: false,
      reminderCadence: 'weekly'
    }).subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/notifications/preferences');
    expect(request.request.method).toBe('PATCH');
    expect(request.request.body).toEqual({
      reminderEmailEnabled: false,
      reminderCadence: 'weekly'
    });

    request.flush({ message: 'Notification preferences updated successfully' });

    expect(responseBody).toBeTruthy();
  });

  it('maps API validation failures to Problem Details with code and traceId', () => {
    let receivedError: unknown;

    service.updatePreferences({ reminderCadence: 'weekly' }).subscribe({
      next: () => fail('Expected validation error'),
      error: (error) => {
        receivedError = error;
      }
    });

    const request = httpMock.expectOne('/api/v1/notifications/preferences');
    request.flush(
      {
        type: 'https://api.tasktracker.local/problems/validation-error',
        title: 'Validation Error',
        status: 400,
        code: 'notifications.preferences.validation_failed',
        traceId: '0HN1NOTIFY123',
        errors: {
          reminderCadence: ['Reminder cadence must be one of: daily, weekly.']
        }
      },
      { status: 400, statusText: 'Bad Request' }
    );

    const problem = (receivedError as HttpErrorResponse).error as { code: string; traceId: string; errors: Record<string, string[]> };
    expect(problem.code).toBe('notifications.preferences.validation_failed');
    expect(problem.traceId).toBe('0HN1NOTIFY123');
    expect(problem.errors['reminderCadence'][0]).toContain('daily, weekly');
  });
});
