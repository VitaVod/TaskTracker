import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LeaderboardResponse } from '../../shared/models/leaderboard.models';
import { AuthService } from '../../shared/services/auth.service';
import { LeaderboardService } from '../../shared/services/leaderboard.service';
import { LeaderboardComponent } from './leaderboard.component';

describe('LeaderboardComponent', () => {
  let fixture: ComponentFixture<LeaderboardComponent>;
  let component: LeaderboardComponent;
  let leaderboardService: jasmine.SpyObj<LeaderboardService>;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    leaderboardService = jasmine.createSpyObj<LeaderboardService>('LeaderboardService', ['getLeaderboard']);
    leaderboardService.getLeaderboard.and.returnValue(of(buildResponse('streak', 1, true)));
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['hasRole', 'getCurrentUserId']);
    authService.hasRole.and.returnValue(false);
    authService.getCurrentUserId.and.returnValue(null);

    await TestBed.configureTestingModule({
      imports: [LeaderboardComponent],
      providers: [
        { provide: LeaderboardService, useValue: leaderboardService },
        { provide: AuthService, useValue: authService },
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(LeaderboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads streak leaderboard on init', () => {
    expect(leaderboardService.getLeaderboard).toHaveBeenCalledWith('streak', 1, 20);
    expect(component.state).toBe('ready');
    expect(component.entries.length).toBe(2);
  });

  it('switches leaderboard type and resets to first page', () => {
    component.page = 3;
    leaderboardService.getLeaderboard.calls.reset();
    leaderboardService.getLeaderboard.and.returnValue(of(buildResponse('completedTasks', 1, false)));

    component.selectType('completedTasks');

    expect(component.selectedType).toBe('completedTasks');
    expect(component.page).toBe(1);
    expect(leaderboardService.getLeaderboard).toHaveBeenCalledWith('completedTasks', 1, 20);
  });

  it('supports keyboard selection for type controls', () => {
    leaderboardService.getLeaderboard.calls.reset();
    leaderboardService.getLeaderboard.and.returnValue(of(buildResponse('completedTasks', 1, false)));

    const preventDefault = jasmine.createSpy('preventDefault');
    component.setTypeFromKeyboard({ preventDefault } as unknown as Event, 'completedTasks');

    expect(preventDefault).toHaveBeenCalled();
    expect(component.selectedType).toBe('completedTasks');
    expect(leaderboardService.getLeaderboard).toHaveBeenCalledWith('completedTasks', 1, 20);
  });

  it('moves to next page and previous page with deterministic query params', () => {
    leaderboardService.getLeaderboard.calls.reset();
    leaderboardService.getLeaderboard.and.returnValues(
      of(buildResponse('streak', 2, true)),
      of(buildResponse('streak', 1, true))
    );

    component.nextPage();
    component.previousPage();

    expect(leaderboardService.getLeaderboard.calls.argsFor(0)).toEqual(['streak', 2, 20]);
    expect(leaderboardService.getLeaderboard.calls.argsFor(1)).toEqual(['streak', 1, 20]);
  });

  it('shows empty state when leaderboard has no rows', () => {
    leaderboardService.getLeaderboard.and.returnValue(
      of({
        type: 'streak',
        page: 1,
        pageSize: 20,
        totalCount: 0,
        hasNextPage: false,
        items: []
      })
    );

    fixture = TestBed.createComponent(LeaderboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.state).toBe('empty');
  });

  it('shows error state and support details when request fails', () => {
    leaderboardService.getLeaderboard.and.returnValue(
      throwError(() => ({ code: 'leaderboard.request.failed', traceId: 'trace-42' }))
    );

    fixture = TestBed.createComponent(LeaderboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.state).toBe('error');
    expect(component.errorSupportText).toContain('leaderboard.request.failed');
    expect(component.errorSupportText).toContain('trace-42');
  });

  it('renders privacy-safe identity values exactly as returned by the API', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('anon-12ab34cd');
    expect(compiled.textContent).toContain('Anonymous participant');
  });

  it('adds profile navigation links only for public participants', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const linkedIdentity = compiled.querySelector('a.identity-name');
    const anonymousIdentity = Array.from(compiled.querySelectorAll('.identity-name'))
      .find((node) => node.textContent?.includes('anon-12ab34cd'));

    expect(linkedIdentity?.getAttribute('href')).toContain('/profile/public/p-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa');
    expect(anonymousIdentity?.tagName.toLowerCase()).toBe('p');
  });

  it('routes to my profile when the clicked public entry belongs to the current user', () => {
    authService.getCurrentUserId.and.returnValue('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa');

    fixture = TestBed.createComponent(LeaderboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const linkedIdentity = compiled.querySelector('a.identity-name');

    expect(linkedIdentity?.getAttribute('href')).toContain('/my-profile');
  });

  it('renders movement and pagination accessibility labels', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const movement = compiled.querySelector('.movement-indicator');
    const previousButton = compiled.querySelector('button[aria-label^="Go to previous page"]');
    const nextButton = compiled.querySelector('button[aria-label^="Go to next page"]');

    expect(movement?.getAttribute('aria-label')).toContain('no movement data available');
    expect(previousButton).not.toBeNull();
    expect(nextButton).not.toBeNull();
  });
});

function buildResponse(type: 'streak' | 'completedTasks', page: number, hasNextPage: boolean): LeaderboardResponse {
  return {
    type,
    page,
    pageSize: 20,
    totalCount: 42,
    hasNextPage,
    items: [
      {
        rank: 1,
        publicIdentity: 'anon-12ab34cd',
        identityMode: 'anonymous',
        avatarMarker: 'A1',
        metricValue: 14,
        publicProfileHandle: null
      },
      {
        rank: 2,
        publicIdentity: 'SkyPilot',
        identityMode: 'public',
        avatarMarker: 'B2',
        metricValue: 12,
        publicProfileHandle: 'p-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa'
      }
    ]
  };
}
