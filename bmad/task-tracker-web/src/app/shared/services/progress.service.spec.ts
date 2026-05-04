import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ProgressService } from './progress.service';

describe('ProgressService', () => {
  let service: ProgressService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ProgressService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(ProgressService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('requests xp summary from the progress endpoint', () => {
    let responseBody: unknown;

    service.getXpSummary().subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/progress/xp-summary');
    expect(request.request.method).toBe('GET');

    request.flush({
      totalXp: 120,
      ledgerEntryCount: 12,
      lastGrantedAtUtc: '2026-04-28T09:15:00Z',
      levelProgress: {
        currentLevel: 2,
        currentLevelThresholdXp: 100,
        nextLevel: 3,
        nextLevelThresholdXp: 225,
        percentToNextLevel: 16,
        bandMilestoneLevels: [3, 5, 10, 20, 30, 50],
        reachedBandCount: 0,
        nextBandLevel: 3
      },
      outcomeExplanation: {
        reasonCode: 'xp-earned-from-completions',
        message: 'XP increased from eligible task completion events processed by the progression engine.'
      }
    });

    expect(responseBody).toBeTruthy();
  });

  it('requests streak snapshot from the progress endpoint', () => {
    let responseBody: unknown;

    service.getStreakSnapshot().subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/progress/streak');
    expect(request.request.method).toBe('GET');

    request.flush({
      outcome: 'continue',
      currentStreakDays: 5,
      longestStreakDays: 11,
      timeZoneId: 'UTC',
      evaluationWindowStartUtc: '2026-04-27T00:00:00Z',
      evaluationWindowEndUtc: '2026-04-28T00:00:00Z',
      lastEvaluatedAtUtc: '2026-04-28T09:15:00Z',
      isRecoveryPromptVisible: false,
      recoveryReason: null,
      recommendedAction: null,
      outcomeExplanation: {
        reasonCode: 'streak-continued',
        message: 'Your streak is active at 5 day(s) because completions stayed within the allowed local-day window.'
      },
      recoveryExplanation: null
    });

    expect(responseBody).toBeTruthy();
  });

  it('requests trend summary with explicit granularity and window', () => {
    let responseBody: unknown;

    service.getTrendSummary('weekly', 56).subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/progress/trend?granularity=weekly&windowDays=56');
    expect(request.request.method).toBe('GET');

    request.flush({
      granularity: 'weekly',
      windowDays: 56,
      timeZoneId: 'UTC',
      rangeStartUtc: '2026-03-02T00:00:00Z',
      rangeEndUtc: '2026-04-27T00:00:00Z',
      items: [
        {
          bucketStartUtc: '2026-04-20T00:00:00Z',
          bucketEndUtc: '2026-04-27T00:00:00Z',
          completedTaskCount: 6,
          xpGranted: 60
        }
      ]
    });

    expect(responseBody).toBeTruthy();
  });

  it('maps errors to progress problem details with code and traceId', () => {
    let receivedError: unknown;

    service.getTrendSummary('daily', 365).subscribe({
      next: () => fail('Expected validation error'),
      error: (error) => {
        receivedError = error;
      }
    });

    const request = httpMock.expectOne('/api/v1/progress/trend?granularity=daily&windowDays=365');
    request.flush(
      {
        type: 'https://api.tasktracker.local/problems/validation',
        title: 'Validation failed',
        status: 400,
        code: 'validation.request.invalid',
        traceId: '0HN1PROGRESS123',
        errors: {
          windowDays: ['The windowDays field must be between 7 and 90.']
        }
      },
      { status: 400, statusText: 'Bad Request' }
    );

    const problem = receivedError as { code: string; traceId: string; errors: Record<string, string[]> };
    expect(problem.code).toBe('validation.request.invalid');
    expect(problem.traceId).toBe('0HN1PROGRESS123');
    expect(problem.errors['windowDays'][0]).toContain('between 7 and 90');
  });
});
