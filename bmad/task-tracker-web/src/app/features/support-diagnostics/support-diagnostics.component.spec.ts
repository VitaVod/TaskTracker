import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import {
  PrivilegedAuditResponse,
  SupportTimelineResponse,
  SupportUserDiagnosticsResponse
} from '../../shared/models/support-diagnostics.models';
import { SupportDiagnosticsService } from '../../shared/services/support-diagnostics.service';
import { SupportDiagnosticsComponent } from './support-diagnostics.component';

describe('SupportDiagnosticsComponent', () => {
  let fixture: ComponentFixture<SupportDiagnosticsComponent>;
  let component: SupportDiagnosticsComponent;
  let supportDiagnosticsService: jasmine.SpyObj<SupportDiagnosticsService>;

  beforeEach(async () => {
    supportDiagnosticsService = jasmine.createSpyObj<SupportDiagnosticsService>('SupportDiagnosticsService', [
      'getUserDiagnostics',
      'getUserTimeline',
      'getPrivilegedAudits'
    ]);
    supportDiagnosticsService.getUserDiagnostics.and.returnValue(of(buildResponse()));
    supportDiagnosticsService.getUserTimeline.and.returnValue(of(buildTimelineResponse()));
    supportDiagnosticsService.getPrivilegedAudits.and.returnValue(of(buildPrivilegedAuditResponse()));

    await TestBed.configureTestingModule({
      imports: [SupportDiagnosticsComponent],
      providers: [{ provide: SupportDiagnosticsService, useValue: supportDiagnosticsService }, provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(SupportDiagnosticsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('loads diagnostics when a valid GUID is provided', () => {
    component.targetUserId = 'a3dc1732-f0f9-4a2d-9ec4-5fd57e14d9cf';

    component.loadDiagnostics();

    expect(supportDiagnosticsService.getUserDiagnostics).toHaveBeenCalledWith(
      'a3dc1732-f0f9-4a2d-9ec4-5fd57e14d9cf',
      14,
      25
    );
    expect(supportDiagnosticsService.getUserTimeline).toHaveBeenCalled();
    expect(supportDiagnosticsService.getPrivilegedAudits).toHaveBeenCalled();
    expect(component.state).toBe('ready');
    expect(component.snapshot?.account.displayName).toBe('Support Target');
    expect(component.timeline?.items.length).toBe(2);
  });

  it('renders empty state when no tasks and no markers are present', () => {
    supportDiagnosticsService.getUserDiagnostics.and.returnValue(of(buildResponse(0, 0)));
    supportDiagnosticsService.getUserTimeline.and.returnValue(of(buildTimelineResponse(0)));
    supportDiagnosticsService.getPrivilegedAudits.and.returnValue(of(buildPrivilegedAuditResponse(0)));
    component.targetUserId = 'a3dc1732-f0f9-4a2d-9ec4-5fd57e14d9cf';

    component.loadDiagnostics();

    expect(component.state).toBe('empty');
  });

  it('shows error state with support details when request fails', () => {
    supportDiagnosticsService.getUserTimeline.and.returnValue(
      throwError(() => ({ code: 'authz.access.denied', traceId: 'trace-support-1' }))
    );
    component.targetUserId = 'a3dc1732-f0f9-4a2d-9ec4-5fd57e14d9cf';

    component.loadDiagnostics();

    expect(component.state).toBe('error');
    expect(component.errorSupportText).toContain('authz.access.denied');
    expect(component.errorSupportText).toContain('trace-support-1');
  });

  it('does not call the service for invalid GUID input', () => {
    component.targetUserId = 'not-a-guid';

    component.loadDiagnostics();

    expect(supportDiagnosticsService.getUserDiagnostics).not.toHaveBeenCalled();
    expect(supportDiagnosticsService.getUserTimeline).not.toHaveBeenCalled();
    expect(supportDiagnosticsService.getPrivilegedAudits).not.toHaveBeenCalled();
    expect(component.validationMessage).toContain('valid user ID');
  });
});

function buildResponse(taskCount = 3, markerCount = 2): SupportUserDiagnosticsResponse {
  return {
    account: {
      userId: 'a3dc1732-f0f9-4a2d-9ec4-5fd57e14d9cf',
      email: 'support.target@example.com',
      displayName: 'Support Target',
      role: 'User',
      timeZoneId: 'UTC',
      locale: 'en-US',
      leaderboardParticipationMode: 'public',
      isSuspiciousFlagged: false,
      createdAtUtc: '2026-04-01T10:00:00Z',
      modifiedAtUtc: '2026-04-30T10:00:00Z'
    },
    taskState: {
      totalCount: taskCount,
      completedCount: Math.min(taskCount, 2),
      activeCount: Math.max(taskCount - 2, 0),
      lastCompletedAtUtc: taskCount > 0 ? '2026-04-30T09:30:00Z' : null,
      recentCompletions: taskCount > 0
        ? [
          {
            taskId: '0ef23656-95b1-4f04-a2d7-5bece9c6ab52',
            title: 'Completed task',
            completedAtUtc: '2026-04-30T09:30:00Z'
          }
        ]
        : []
    },
    xpState: {
      totalXp: 120,
      ledgerEntryCount: 5,
      lastGrantedAtUtc: '2026-04-30T09:30:00Z',
      outcomeReasonCode: 'xp-earned-from-completions',
      outcomeExplanation: 'XP increased from eligible completion events.'
    },
    streakState: {
      outcome: 'Continue',
      currentStreakDays: 6,
      longestStreakDays: 8,
      timeZoneId: 'UTC',
      evaluationWindowStartUtc: '2026-04-29T00:00:00Z',
      evaluationWindowEndUtc: '2026-04-30T00:00:00Z',
      lastEvaluatedAtUtc: '2026-04-30T09:30:00Z',
      outcomeReasonCode: 'streak-continued',
      outcomeExplanation: 'Streak remains active.',
      isRecoveryPromptVisible: false,
      recoveryReason: null,
      recommendedAction: null,
      recoveryExplanation: null
    },
    window: {
      windowDays: 14,
      windowStartUtc: '2026-04-16T00:00:00Z',
      markerLimit: 25
    },
    recentMarkers: markerCount > 0
      ? [
        {
          markerType: 'xpLedgerEntry',
          markerId: '99dd2ba8-5678-4cdb-8e20-c8afce5ef243',
          occurredAtUtc: '2026-04-30T09:30:00Z',
          summary: 'TaskCompleted: 25 XP',
          traceId: null,
          correlationRef: 'corr-1'
        }
      ]
      : [],
    correlationId: 'support-diag-corr-1',
    traceId: 'trace-support-diag-1'
  };
}

function buildTimelineResponse(count = 2): SupportTimelineResponse {
  return {
    page: 1,
    pageSize: 50,
    totalCount: count,
    hasNextPage: false,
    filters: {
      eventType: null,
      startUtc: '2026-04-16T00:00:00Z',
      endUtc: '2026-04-30T23:59:59Z'
    },
    items: count > 0
      ? [
        {
          eventId: '99dd2ba8-5678-4cdb-8e20-c8afce5ef243',
          eventType: 'xpLedger',
          occurredAtUtc: '2026-04-30T09:30:00Z',
          sourceSubsystem: 'progression',
          messageCode: 'progress.xp.recorded',
          message: 'TaskCompleted granted 25 XP for task 0ef23656-95b1-4f04-a2d7-5bece9c6ab52.',
          ruleOutcome: 'xpGranted:25',
          traceId: null,
          correlationId: 'corr-1',
          actorContext: 'system',
          targetContext: 'user:a3dc1732f0f94a2d9ec45fd57e14d9cf',
          relatedEntityId: '0ef23656-95b1-4f04-a2d7-5bece9c6ab52'
        },
        {
          eventId: 'f6949ee5-2d90-4f15-b00a-c1f7ae2185df',
          eventType: 'taskCompletion',
          occurredAtUtc: '2026-04-30T08:30:00Z',
          sourceSubsystem: 'progression',
          messageCode: 'task.completion.recorded',
          message: 'Task completion event recorded for task 0ef23656-95b1-4f04-a2d7-5bece9c6ab52.',
          ruleOutcome: 'completed',
          traceId: null,
          correlationId: 'corr-2',
          actorContext: 'system',
          targetContext: 'user:a3dc1732f0f94a2d9ec45fd57e14d9cf',
          relatedEntityId: '0ef23656-95b1-4f04-a2d7-5bece9c6ab52'
        }
      ]
      : [],
    correlationId: 'support-timeline-corr-1',
    traceId: 'trace-support-timeline-1'
  };
}

function buildPrivilegedAuditResponse(count = 1): PrivilegedAuditResponse {
  return {
    page: 1,
    pageSize: 25,
    totalCount: count,
    hasNextPage: false,
    filters: {
      actorUserId: 'ops-admin-1',
      targetUserId: 'a3dc1732-f0f9-4a2d-9ec4-5fd57e14d9cf',
      actionType: 'moderation.apply',
      startUtc: '2026-04-16T00:00:00Z',
      endUtc: '2026-04-30T23:59:59Z'
    },
    items: count > 0
      ? [
        {
          auditId: '5fb2db89-7230-4c72-83d4-d2e00f8d2927',
          actorUserId: 'ops-admin-1',
          actorRole: 'Admin',
          targetUserId: 'a3dc1732-f0f9-4a2d-9ec4-5fd57e14d9cf',
          actionType: 'moderation.apply',
          reasonCode: 'manual-investigation-confirmed',
          reasonText: 'Manual review approved.',
          outcome: 'succeeded',
          occurredAtUtc: '2026-04-30T09:35:00Z',
          correlationId: 'corr-privileged-1',
          traceId: 'trace-privileged-1'
        }
      ]
      : [],
    correlationId: 'corr-privileged-query-1',
    traceId: 'trace-privileged-query-1'
  };
}
