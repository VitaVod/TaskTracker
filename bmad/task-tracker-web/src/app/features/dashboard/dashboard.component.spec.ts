import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { ProgressTrendSummary } from '../../shared/models/progress.models';
import { AuthService } from '../../shared/services/auth.service';
import { ProgressService } from '../../shared/services/progress.service';
import { StatisticsService } from '../../shared/services/statistics.service';
import { DashboardComponent } from './dashboard.component';

describe('DashboardComponent', () => {
  let fixture: ComponentFixture<DashboardComponent>;
  let component: DashboardComponent;
  let authService: jasmine.SpyObj<AuthService>;
  let progressService: jasmine.SpyObj<ProgressService>;
  let statisticsService: jasmine.SpyObj<StatisticsService>;

  beforeEach(async () => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['logout', 'hasRole']);
    progressService = jasmine.createSpyObj<ProgressService>('ProgressService', [
      'getXpSummary',
      'getStreakSnapshot',
      'getTrendSummary'
    ]);
    statisticsService = jasmine.createSpyObj<StatisticsService>('StatisticsService', ['getGlobalStatistics']);

    authService.logout.and.returnValue(of(void 0));
  authService.hasRole.and.returnValue(false);
    progressService.getXpSummary.and.returnValue(of({
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
    }));
    progressService.getStreakSnapshot.and.returnValue(of({
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
    }));
    progressService.getTrendSummary.and.returnValue(of(buildTrendSummary([1, 1, 1, 1, 1, 1, 0, 2, 1, 0, 1, 2, 2, 1])));
    statisticsService.getGlobalStatistics.and.returnValue(of({
      totalTasksCreated: 400,
      totalTasksCompleted: 260
    }));

    await TestBed.configureTestingModule({
      imports: [DashboardComponent],
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: ProgressService, useValue: progressService },
        { provide: StatisticsService, useValue: statisticsService },
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads dashboard progress snapshot on init', () => {
    expect(progressService.getXpSummary).toHaveBeenCalled();
    expect(progressService.getStreakSnapshot).toHaveBeenCalled();
    expect(progressService.getTrendSummary).toHaveBeenCalledWith('daily', 14);
    expect(statisticsService.getGlobalStatistics).toHaveBeenCalled();
    expect(component.progressState).toBe('ready');
    expect(component.momentumState).toBe('ready');
    expect(component.statisticsState).toBe('ready');
    expect(component.xpSummary?.totalXp).toBe(120);
    expect(component.momentumSummary?.totalCompletedInWindow).toBe(15);
    expect(component.globalStatistics?.totalTasksCreated).toBe(400);
  });

  it('hides weekly granularity filter in momentum controls', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    const weeklyOption = compiled.querySelector('option[value="weekly"]');

    expect(weeklyOption).toBeNull();
  });

  it('shows error state when both progress requests fail', () => {
    progressService.getXpSummary.and.returnValue(throwError(() => ({ title: 'Failed' })));
    progressService.getStreakSnapshot.and.returnValue(throwError(() => ({ title: 'Failed' })));

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.progressState).toBe('error');
    expect(component.progressError).toContain('Unable to load progress');
  });

  it('preserves last known progress data when one progress request fails on refresh', () => {
    component.xpSummary = {
      totalXp: 300,
      ledgerEntryCount: 30,
      lastGrantedAtUtc: '2026-04-28T10:00:00Z',
      levelProgress: {
        currentLevel: 3,
        currentLevelThresholdXp: 225,
        nextLevel: 4,
        nextLevelThresholdXp: 375,
        percentToNextLevel: 50,
        bandMilestoneLevels: [3, 5, 10, 20, 30, 50],
        reachedBandCount: 1,
        nextBandLevel: 5
      },
      outcomeExplanation: {
        reasonCode: 'xp-earned-from-completions',
        message: 'XP increased from eligible task completion events processed by the progression engine.'
      }
    };

    component.streakSnapshot = {
      outcome: 'continue',
      currentStreakDays: 8,
      longestStreakDays: 14,
      timeZoneId: 'UTC',
      evaluationWindowStartUtc: '2026-04-27T00:00:00Z',
      evaluationWindowEndUtc: '2026-04-28T00:00:00Z',
      lastEvaluatedAtUtc: '2026-04-28T10:00:00Z',
      isRecoveryPromptVisible: false,
      recoveryReason: null,
      recommendedAction: null,
      outcomeExplanation: {
        reasonCode: 'streak-continued',
        message: 'Your streak is active at 8 day(s) because completions stayed within the allowed local-day window.'
      },
      recoveryExplanation: null
    };

    progressService.getXpSummary.and.returnValue(throwError(() => ({ title: 'Failed' })));
    progressService.getStreakSnapshot.and.returnValue(of({
      outcome: 'continue',
      currentStreakDays: 9,
      longestStreakDays: 14,
      timeZoneId: 'UTC',
      evaluationWindowStartUtc: '2026-04-28T00:00:00Z',
      evaluationWindowEndUtc: '2026-04-29T00:00:00Z',
      lastEvaluatedAtUtc: '2026-04-28T11:00:00Z',
      isRecoveryPromptVisible: false,
      recoveryReason: null,
      recommendedAction: null,
      outcomeExplanation: {
        reasonCode: 'streak-continued',
        message: 'Your streak is active at 9 day(s) because completions stayed within the allowed local-day window.'
      },
      recoveryExplanation: null
    }));

    component.refreshProgress();

    expect(component.progressState).toBe('ready');
    expect(component.xpSummary?.totalXp).toBe(300);
    expect(component.streakSnapshot?.currentStreakDays).toBe(9);
  });

  it('switches to weekly trend with bounded window days', () => {
    component.onGranularityChanged({ target: { value: 'weekly' } } as unknown as Event);

    expect(component.selectedGranularity).toBe('weekly');
    expect(component.selectedWindow).toBe(12);
    expect(progressService.getTrendSummary).toHaveBeenCalledWith('weekly', 84);
  });

  it('uses week-over-week comparison for weekly momentum direction', () => {
    progressService.getTrendSummary.and.returnValue(of({
      granularity: 'weekly',
      windowDays: 84,
      timeZoneId: 'UTC',
      rangeStartUtc: '2026-02-01T00:00:00Z',
      rangeEndUtc: '2026-04-28T00:00:00Z',
      items: [
        { bucketStartUtc: '2026-03-03T00:00:00Z', bucketEndUtc: '2026-03-10T00:00:00Z', completedTaskCount: 2, xpGranted: 20 },
        { bucketStartUtc: '2026-03-10T00:00:00Z', bucketEndUtc: '2026-03-17T00:00:00Z', completedTaskCount: 2, xpGranted: 20 },
        { bucketStartUtc: '2026-03-17T00:00:00Z', bucketEndUtc: '2026-03-24T00:00:00Z', completedTaskCount: 2, xpGranted: 20 },
        { bucketStartUtc: '2026-03-24T00:00:00Z', bucketEndUtc: '2026-03-31T00:00:00Z', completedTaskCount: 2, xpGranted: 20 },
        { bucketStartUtc: '2026-03-31T00:00:00Z', bucketEndUtc: '2026-04-07T00:00:00Z', completedTaskCount: 2, xpGranted: 20 },
        { bucketStartUtc: '2026-04-07T00:00:00Z', bucketEndUtc: '2026-04-14T00:00:00Z', completedTaskCount: 2, xpGranted: 20 },
        { bucketStartUtc: '2026-04-14T00:00:00Z', bucketEndUtc: '2026-04-21T00:00:00Z', completedTaskCount: 8, xpGranted: 80 },
        { bucketStartUtc: '2026-04-21T00:00:00Z', bucketEndUtc: '2026-04-28T00:00:00Z', completedTaskCount: 1, xpGranted: 10 }
      ]
    }));

    component.onGranularityChanged({ target: { value: 'weekly' } } as unknown as Event);

    expect(component.momentumSummary?.direction).toBe('down');
    expect(component.momentumSummary?.directionLabel).toBe('Down by 7 completions');
  });

  it('shows empty momentum state when selected window has no completions', () => {
    progressService.getTrendSummary.and.returnValue(of(buildTrendSummary([0, 0, 0, 0, 0, 0, 0])));

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.momentumState).toBe('empty');
    expect(component.momentumSummary?.totalCompletedInWindow).toBe(0);
  });

  it('shows momentum error state when trend request fails', () => {
    progressService.getTrendSummary.and.returnValue(throwError(() => ({ title: 'Failed' })));

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.momentumState).toBe('error');
    expect(component.momentumError).toContain('Unable to load momentum history');
  });

  it('maps trend delta into text and icon so meaning is not color-only', () => {
    expect(component.momentumSummary?.directionLabel).toBe('Up by 3 completions');
    expect(component.trendDirectionIcon(component.momentumSummary?.direction)).toBe('^');
  });

  it('routes to day detail when a trend card is selected', () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    const compiled = fixture.nativeElement as HTMLElement;
    const trendCard = compiled.querySelector('.trend-card') as HTMLButtonElement;
    trendCard.click();

    expect(navigateSpy).toHaveBeenCalledWith(['/dashboard/day', '2026-04-01']);
  });

  it('supports keyboard activation for trend-card day navigation', () => {
    const router = TestBed.inject(Router);
    const navigateSpy = spyOn(router, 'navigate').and.resolveTo(true);

    const keyboardEvent = new KeyboardEvent('keydown', { key: 'Enter' });
    component.onTrendItemKeydown(keyboardEvent, '2026-04-03T00:00:00Z');

    expect(navigateSpy).toHaveBeenCalledWith(['/dashboard/day', '2026-04-03']);
  });

  it('maps streak status labels from snapshot outcomes', () => {
    component.streakSnapshot = {
      outcome: 'restart',
      currentStreakDays: 1,
      longestStreakDays: 11,
      timeZoneId: 'UTC',
      evaluationWindowStartUtc: '2026-04-27T00:00:00Z',
      evaluationWindowEndUtc: '2026-04-28T00:00:00Z',
      lastEvaluatedAtUtc: '2026-04-28T09:15:00Z',
      isRecoveryPromptVisible: true,
      recoveryReason: 'streak-restarted',
      recommendedAction: 'maintain-tomorrow',
      outcomeExplanation: {
        reasonCode: 'streak-restarted',
        message: 'Your streak restarted after a missed continuity window and now counts from your latest eligible completion.'
      },
      recoveryExplanation: {
        reasonCode: 'streak-restarted',
        message: 'Your streak has restarted. Completing at least one eligible task in the next local-day window keeps it active.'
      }
    };

    expect(component.streakStatusLabel()).toBe('Continuity restarted');
  });

  it('renders recovery prompt with deterministic missed-day guidance', () => {
    progressService.getStreakSnapshot.and.returnValue(of({
      outcome: 'continue',
      currentStreakDays: 6,
      longestStreakDays: 11,
      timeZoneId: 'UTC',
      evaluationWindowStartUtc: '2026-04-24T00:00:00Z',
      evaluationWindowEndUtc: '2026-04-25T00:00:00Z',
      lastEvaluatedAtUtc: '2026-04-25T09:15:00Z',
      isRecoveryPromptVisible: true,
      recoveryReason: 'missed-day-detected',
      recommendedAction: 'complete-task-today',
      outcomeExplanation: {
        reasonCode: 'streak-continued',
        message: 'Your streak is active at 6 day(s) because completions stayed within the allowed local-day window.'
      },
      recoveryExplanation: {
        reasonCode: 'missed-day-detected',
        message: 'A missed local day was detected. Completing one eligible task today starts the next streak immediately.'
      }
    }));

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    const recoveryCard = compiled.querySelector('.recovery-card');
    expect(recoveryCard).not.toBeNull();
    expect(recoveryCard?.textContent).toContain('Recovery prompt');
    expect(recoveryCard?.textContent).toContain('A missed local day was detected');
    expect(recoveryCard?.textContent).toContain('Complete a task now');
  });

  it('hides recovery prompt when server signal is not visible', () => {
    component.streakSnapshot = {
      outcome: 'continue',
      currentStreakDays: 8,
      longestStreakDays: 14,
      timeZoneId: 'UTC',
      evaluationWindowStartUtc: '2026-04-27T00:00:00Z',
      evaluationWindowEndUtc: '2026-04-28T00:00:00Z',
      lastEvaluatedAtUtc: '2026-04-28T10:00:00Z',
      isRecoveryPromptVisible: false,
      recoveryReason: null,
      recommendedAction: null,
      outcomeExplanation: {
        reasonCode: 'streak-continued',
        message: 'Your streak is active at 8 day(s) because completions stayed within the allowed local-day window.'
      },
      recoveryExplanation: null
    };

    fixture.detectChanges();

    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.recovery-card')).toBeNull();
  });

  it('shows statistics error state when global stats request fails', () => {
    statisticsService.getGlobalStatistics.and.returnValue(throwError(() => ({ title: 'Failed' })));

    fixture = TestBed.createComponent(DashboardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.statisticsState).toBe('error');
    expect(component.statisticsError).toContain('Unable to load global task activity');
  });

  it('formats completion rate from global statistics snapshot', () => {
    component.globalStatistics = {
      totalTasksCreated: 8,
      totalTasksCompleted: 3
    };

    expect(component.completionRateLabel()).toBe('38%');
  });

  it('renders server-provided XP and streak explanation text', () => {
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('XP increased from eligible task completion events processed by the progression engine.');
    expect(compiled.textContent).toContain('Your streak is active at 5 day(s) because completions stayed within the allowed local-day window.');
  });

  it('renders XP level card with non-color-only milestone cues', () => {
    const compiled = fixture.nativeElement as HTMLElement;

    expect(compiled.textContent).toContain('Level 2');
    expect(compiled.textContent).toContain('Next level 3 at 225 XP');

    const activeBand = compiled.querySelector('.xp-band-item[data-state="active"]');
    expect(activeBand?.textContent).toContain('Level 3');
    expect(activeBand?.textContent).toContain('>');
  });

  it('ignores stale progress refresh result and keeps latest state', () => {
    progressService.getXpSummary.and.returnValues(
      of({
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
      }),
      of({
        totalXp: 180,
        ledgerEntryCount: 18,
        lastGrantedAtUtc: '2026-04-28T10:15:00Z',
        levelProgress: {
          currentLevel: 2,
          currentLevelThresholdXp: 100,
          nextLevel: 3,
          nextLevelThresholdXp: 225,
          percentToNextLevel: 64,
          bandMilestoneLevels: [3, 5, 10, 20, 30, 50],
          reachedBandCount: 0,
          nextBandLevel: 3
        },
        outcomeExplanation: {
          reasonCode: 'xp-earned-from-completions',
          message: 'XP increased from eligible task completion events processed by the progression engine.'
        }
      })
    );

    component.refreshProgress();
    component.refreshProgress();

    expect(component.xpSummary?.totalXp).toBe(180);
    expect(component.xpSummary?.levelProgress.percentToNextLevel).toBe(64);
  });

  it('ignores stale momentum refresh result and keeps latest trend snapshot', () => {
    const firstTrendResponse = new Subject<ProgressTrendSummary>();
    const secondTrendResponse = new Subject<ProgressTrendSummary>();

    progressService.getTrendSummary.and.returnValues(
      firstTrendResponse.asObservable(),
      secondTrendResponse.asObservable()
    );

    component.refreshMomentum();
    component.refreshMomentum();

    secondTrendResponse.next(buildTrendSummary([1, 1, 1, 1, 1, 1, 1]));
    secondTrendResponse.complete();

    firstTrendResponse.next(buildTrendSummary([9, 9, 9, 9, 9, 9, 9]));
    firstTrendResponse.complete();

    expect(component.momentumSummary?.totalCompletedInWindow).toBe(7);
    expect(component.momentumSummary?.recentCompletions).toBe(7);
  });
});

function buildTrendSummary(completedCounts: number[]): ProgressTrendSummary {
  return {
    granularity: 'daily',
    windowDays: 30,
    timeZoneId: 'UTC',
    rangeStartUtc: '2026-04-01T00:00:00Z',
    rangeEndUtc: '2026-04-28T00:00:00Z',
    items: completedCounts.map((count, index) => ({
      bucketStartUtc: `2026-04-${String(index + 1).padStart(2, '0')}T00:00:00Z`,
      bucketEndUtc: `2026-04-${String(index + 1).padStart(2, '0')}T23:59:59Z`,
      completedTaskCount: count,
      xpGranted: count * 10
    }))
  };
}
