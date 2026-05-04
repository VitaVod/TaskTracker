import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ModerationActionResponse, SuspiciousCasesResponse } from '../../shared/models/suspicious-cases.models';
import { SuspiciousCasesService } from '../../shared/services/suspicious-cases.service';
import { OpsSuspiciousCasesComponent } from './ops-suspicious-cases.component';

describe('OpsSuspiciousCasesComponent', () => {
  let fixture: ComponentFixture<OpsSuspiciousCasesComponent>;
  let component: OpsSuspiciousCasesComponent;
  let suspiciousCasesService: jasmine.SpyObj<SuspiciousCasesService>;

  beforeEach(async () => {
    suspiciousCasesService = jasmine.createSpyObj<SuspiciousCasesService>('SuspiciousCasesService', [
      'getCases',
      'applyModerationAction'
    ]);
    suspiciousCasesService.getCases.and.returnValue(of(buildResponse(2, true)));
    suspiciousCasesService.applyModerationAction.and.returnValue(of(buildModerationResponse('flagEntity', 'succeeded')));

    await TestBed.configureTestingModule({
      imports: [OpsSuspiciousCasesComponent],
      providers: [{ provide: SuspiciousCasesService, useValue: suspiciousCasesService }, provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(OpsSuspiciousCasesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads suspicious cases on init', () => {
    expect(suspiciousCasesService.getCases).toHaveBeenCalledWith('all', 1, 20);
    expect(component.state).toBe('ready');
    expect(component.cases.length).toBe(2);
  });

  it('shows empty state when no cases are returned', () => {
    suspiciousCasesService.getCases.and.returnValue(of(buildResponse(0, false)));

    fixture = TestBed.createComponent(OpsSuspiciousCasesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.state).toBe('empty');
  });

  it('shows error state and support details when request fails', () => {
    suspiciousCasesService.getCases.and.returnValue(
      throwError(() => ({ code: 'authz.access.denied', traceId: 'trace-ops-1' }))
    );

    fixture = TestBed.createComponent(OpsSuspiciousCasesComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();

    expect(component.state).toBe('error');
    expect(component.errorSupportText).toContain('authz.access.denied');
    expect(component.errorSupportText).toContain('trace-ops-1');
  });

  it('switches filter and resets page', () => {
    component.page = 3;
    suspiciousCasesService.getCases.calls.reset();
    suspiciousCasesService.getCases.and.returnValue(of(buildResponse(1, false)));

    component.selectFilter('activitySpike');

    expect(component.selectedFilter).toBe('activitySpike');
    expect(component.page).toBe(1);
    expect(suspiciousCasesService.getCases).toHaveBeenCalledWith('activitySpike', 1, 20);
  });

  it('supports pagination controls', () => {
    suspiciousCasesService.getCases.calls.reset();
    component.state = 'ready';
    component.hasNextPage = true;
    component.page = 1;
    suspiciousCasesService.getCases.and.returnValues(
      of(buildResponse(2, true, 2)),
      of(buildResponse(2, false, 1))
    );

    component.nextPage();
    component.previousPage();

    expect(suspiciousCasesService.getCases.calls.argsFor(0)).toEqual(['all', 2, 20]);
    expect(suspiciousCasesService.getCases.calls.argsFor(1)).toEqual(['all', 1, 20]);
  });

  it('requires explicit confirmation before ranking correction submit', () => {
    const targetCase = component.cases[0];

    component.queueRankingCorrection(targetCase);
    component.confirmRankingCorrection();

    expect(suspiciousCasesService.applyModerationAction).not.toHaveBeenCalled();
  });

  it('submits ranking correction when confirmation is checked', () => {
    suspiciousCasesService.applyModerationAction.and.returnValue(
      of(buildModerationResponse('rankingCorrection', 'succeeded'))
    );

    const targetCase = component.cases[0];
    component.queueRankingCorrection(targetCase);
    component.confirmationChecked = true;

    component.confirmRankingCorrection();

    expect(suspiciousCasesService.applyModerationAction).toHaveBeenCalledWith(
      targetCase.caseId,
      jasmine.objectContaining({
        actionType: 'rankingCorrection',
        confirmDestructive: true,
        confirmationToken: targetCase.destructiveConfirmationToken
      })
    );
    expect(component.actionFeedback).toContain('Ranking correction applied');
  });

  it('submits non-destructive flag action without confirmation dialog', () => {
    const targetCase = component.cases[1];

    component.submitFlagEntity(targetCase);

    expect(suspiciousCasesService.applyModerationAction).toHaveBeenCalledWith(
      targetCase.caseId,
      jasmine.objectContaining({
        actionType: 'flagEntity',
        confirmDestructive: false,
        confirmationToken: null
      })
    );
  });
});

function buildResponse(itemsCount: number, hasNextPage: boolean, page = 1): SuspiciousCasesResponse {
  const items = itemsCount === 0
    ? []
    : [
      {
        caseId: 'ranking-mismatch-1',
        publicIdentity: 'anon-abc123',
        identityMode: 'anonymous' as const,
        anomalyType: 'rankingMismatch' as const,
        signalSummary: '12 total completions with a 0-day current streak.',
        severity: 76,
        detectedAtUtc: '2026-04-30T10:00:00Z',
        lastActivityAtUtc: '2026-04-30T09:58:00Z',
        correlationRef: 'corr-ranking-1',
        destructiveConfirmationToken: 'token-ranking-1'
      },
      {
        caseId: 'activity-spike-1',
        publicIdentity: 'OpsUser',
        identityMode: 'public' as const,
        anomalyType: 'activitySpike' as const,
        signalSummary: '5 completions in the last 7 days.',
        severity: 75,
        detectedAtUtc: '2026-04-30T09:00:00Z',
        lastActivityAtUtc: '2026-04-30T08:59:00Z',
        correlationRef: 'corr-activity-1',
        destructiveConfirmationToken: null
      }
    ];

  return {
    page,
    pageSize: 20,
    totalCount: itemsCount,
    hasNextPage,
    items: items.slice(0, itemsCount)
  };
}

function buildModerationResponse(
  actionType: 'rankingCorrection' | 'flagEntity',
  outcome: 'succeeded' | 'alreadyApplied'
): ModerationActionResponse {
  return {
    auditId: '2e53f14f-31f4-4962-8bc9-8d8452bc46f0',
    caseId: 'ranking-mismatch-1',
    actionType,
    outcome,
    correlationRef: 'corr-ranking-1',
    processedAtUtc: '2026-04-30T10:00:00Z',
    traceId: 'trace-ops-moderation-1'
  };
}
