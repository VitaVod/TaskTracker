import { ComponentFixture, TestBed } from '@angular/core/testing';
import { convertToParamMap, provideRouter } from '@angular/router';
import { ActivatedRoute } from '@angular/router';
import { of, throwError } from 'rxjs';
import { LeaderboardService } from '../../shared/services/leaderboard.service';
import { PublicProfileComponent } from './public-profile.component';

describe('PublicProfileComponent', () => {
  let fixture: ComponentFixture<PublicProfileComponent>;
  let component: PublicProfileComponent;
  let leaderboardService: jasmine.SpyObj<LeaderboardService>;

  beforeEach(async () => {
    leaderboardService = jasmine.createSpyObj<LeaderboardService>('LeaderboardService', ['getPublicProfile']);
    leaderboardService.getPublicProfile.and.returnValue(of({
      visibility: 'public',
      publicIdentity: 'Sky Pilot',
      avatarMarker: 'avatar-abc123',
      statistics: {
        currentStreakDays: 7,
        longestStreakDays: 14,
        completedTaskCount: 21,
        totalXp: 480,
        lastCompletedAtUtc: '2026-05-03T14:00:00Z'
      },
      message: null
    }));

    await TestBed.configureTestingModule({
      imports: [PublicProfileComponent],
      providers: [
        provideRouter([]),
        {
          provide: LeaderboardService,
          useValue: leaderboardService
        },
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              paramMap: convertToParamMap({ handle: 'p-11111111111111111111111111111111' })
            }
          }
        }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(PublicProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads and renders public profile statistics', () => {
    expect(leaderboardService.getPublicProfile).toHaveBeenCalledWith('p-11111111111111111111111111111111');
    expect(component.state).toBe('public');
    expect(component.profile?.statistics?.currentStreakDays).toBe(7);
  });

  it('renders anonymous state when API returns anonymous visibility', () => {
    leaderboardService.getPublicProfile.and.returnValue(of({
      visibility: 'anonymous',
      publicIdentity: null,
      avatarMarker: null,
      statistics: null,
      message: 'Anonymous participant'
    }));

    fixture = TestBed.createComponent(PublicProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.state).toBe('anonymous');
    expect(component.profile?.statistics).toBeNull();
  });

  it('renders error details on request failure', () => {
    leaderboardService.getPublicProfile.and.returnValue(
      throwError(() => ({ code: 'leaderboard.profile.request.failed', traceId: 'trace-88' }))
    );

    fixture = TestBed.createComponent(PublicProfileComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.state).toBe('error');
    expect(component.errorSupportText).toContain('leaderboard.profile.request.failed');
    expect(component.errorSupportText).toContain('trace-88');
  });
});
