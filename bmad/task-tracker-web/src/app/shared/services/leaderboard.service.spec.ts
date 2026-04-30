import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LeaderboardService } from './leaderboard.service';

describe('LeaderboardService', () => {
  let service: LeaderboardService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        LeaderboardService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(LeaderboardService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('requests streak leaderboard with pagination query', () => {
    let responseBody: unknown;

    service.getLeaderboard('streak', 2, 10).subscribe((response) => {
      responseBody = response;
    });

    const request = httpMock.expectOne('/api/v1/leaderboards?type=streak&page=2&pageSize=10');
    expect(request.request.method).toBe('GET');

    request.flush({
      type: 'streak',
      page: 2,
      pageSize: 10,
      totalCount: 35,
      hasNextPage: true,
      items: [
        {
          rank: 11,
          publicIdentity: 'anon-12ab34cd',
          identityMode: 'anonymous',
          avatarMarker: 'avatar-12ab',
          metricValue: 6
        }
      ]
    });

    expect(responseBody).toBeTruthy();
  });

  it('normalizes leaderboard problem details payload', () => {
    let receivedError: unknown;

    service.getLeaderboard('completedTasks', 0, 500).subscribe({
      next: () => fail('Expected validation error'),
      error: (error) => {
        receivedError = error;
      }
    });

    const request = httpMock.expectOne('/api/v1/leaderboards?type=completedTasks&page=0&pageSize=500');
    request.flush(
      {
        type: 'https://api.tasktracker.local/problems/validation',
        title: 'Validation failed',
        status: 400,
        code: 'validation.request.invalid',
        traceId: '0HN1LEADERBOARD123',
        errors: {
          page: ['The page field must be greater than or equal to 1.']
        }
      },
      { status: 400, statusText: 'Bad Request' }
    );

    const problem = receivedError as { code: string; traceId: string; errors: Record<string, string[]> };
    expect(problem.code).toBe('validation.request.invalid');
    expect(problem.traceId).toBe('0HN1LEADERBOARD123');
    expect(problem.errors['page'][0]).toContain('greater than or equal to 1');
  });
});
