import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  PrivilegedAuditResponse,
  SupportDiagnosticsProblemDetails,
  SupportTimelineEventType,
  SupportTimelineResponse,
  SupportUserDiagnosticsResponse
} from '../../shared/models/support-diagnostics.models';
import {
  PrivilegedAuditQueryOptions,
  SupportDiagnosticsService,
  SupportTimelineQueryOptions
} from '../../shared/services/support-diagnostics.service';

type SupportDiagnosticsState = 'idle' | 'loading' | 'ready' | 'empty' | 'error';

@Component({
  selector: 'app-support-diagnostics',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './support-diagnostics.component.html',
  styleUrl: './support-diagnostics.component.scss'
})
export class SupportDiagnosticsComponent {
  private readonly supportDiagnosticsService = inject(SupportDiagnosticsService);

  readonly defaultWindowDays = 14;
  readonly defaultMarkerLimit = 25;
  readonly defaultTimelineMaxItems = 50;
  readonly defaultPrivilegedAuditPageSize = 25;
  readonly timelineEventTypes: Array<{ label: string; value: SupportTimelineEventType | 'all' }> = [
    { label: 'All event types', value: 'all' },
    { label: 'Task completion', value: 'taskCompletion' },
    { label: 'XP ledger', value: 'xpLedger' },
    { label: 'Moderation', value: 'moderation' },
    { label: 'Streak evaluation', value: 'streakEvaluation' }
  ];

  state: SupportDiagnosticsState = 'idle';
  targetUserId = '';
  windowDays = this.defaultWindowDays;
  markerLimit = this.defaultMarkerLimit;
  timelineEventType: SupportTimelineEventType | 'all' = 'all';
  timelineStartDate = this.toDateInputValue(new Date(Date.now() - this.defaultWindowDays * 24 * 60 * 60 * 1000));
  timelineEndDate = this.toDateInputValue(new Date());
  timelineMaxItems = this.defaultTimelineMaxItems;
  auditActorUserId = '';
  auditActionType = '';
  auditStartDate = this.toDateInputValue(new Date(Date.now() - this.defaultWindowDays * 24 * 60 * 60 * 1000));
  auditEndDate = this.toDateInputValue(new Date());
  auditPageSize = this.defaultPrivilegedAuditPageSize;

  snapshot: SupportUserDiagnosticsResponse | null = null;
  timeline: SupportTimelineResponse | null = null;
  privilegedAudits: PrivilegedAuditResponse | null = null;
  errorMessage = '';
  errorSupportText = '';
  validationMessage = '';

  loadDiagnostics(): void {
    const normalizedUserId = this.targetUserId.trim();
    this.validationMessage = '';

    if (!this.isGuid(normalizedUserId)) {
      this.state = 'idle';
      this.snapshot = null;
      this.timeline = null;
      this.privilegedAudits = null;
      this.validationMessage = 'Enter a valid user ID (GUID format) to load diagnostics.';
      return;
    }

    const timelineQuery = this.buildTimelineQuery();
    const privilegedAuditQuery = this.buildPrivilegedAuditQuery(normalizedUserId);
    if (!timelineQuery || !privilegedAuditQuery) {
      this.state = 'idle';
      this.snapshot = null;
      this.timeline = null;
      this.privilegedAudits = null;
      return;
    }

    this.state = 'loading';
    this.snapshot = null;
    this.timeline = null;
    this.privilegedAudits = null;
    this.errorMessage = '';
    this.errorSupportText = '';

    forkJoin({
      snapshot: this.supportDiagnosticsService.getUserDiagnostics(normalizedUserId, this.windowDays, this.markerLimit),
      timeline: this.supportDiagnosticsService.getUserTimeline(normalizedUserId, timelineQuery),
      privilegedAudits: this.supportDiagnosticsService.getPrivilegedAudits(privilegedAuditQuery)
    })
      .subscribe({
        next: (response) => {
          this.snapshot = response.snapshot;
          this.timeline = response.timeline;
          this.privilegedAudits = response.privilegedAudits;
          this.state = this.hasNoDiagnosticData(response.snapshot, response.timeline, response.privilegedAudits) ? 'empty' : 'ready';
        },
        error: (problem: SupportDiagnosticsProblemDetails) => {
          this.state = 'error';
          this.errorMessage = 'Unable to load support diagnostics timeline right now. Verify filters and try again.';
          const supportParts = [problem.code, problem.traceId].filter((value): value is string => Boolean(value));
          this.errorSupportText = supportParts.length > 0 ? `Support: ${supportParts.join(' | ')}` : '';
        }
      });
  }

  formatDate(value: string | null): string {
    if (!value) {
      return 'Not available';
    }

    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) {
      return value;
    }

    return parsed.toLocaleString();
  }

  trackMarker(index: number): number {
    return index;
  }

  trackTimelineEvent(index: number): number {
    return index;
  }

  trackAuditItem(index: number): number {
    return index;
  }

  private hasNoDiagnosticData(
    snapshot: SupportUserDiagnosticsResponse,
    timeline: SupportTimelineResponse,
    privilegedAudits: PrivilegedAuditResponse): boolean {
    return snapshot.taskState.totalCount === 0
      && snapshot.recentMarkers.length === 0
      && timeline.items.length === 0
      && privilegedAudits.items.length === 0;
  }

  private buildTimelineQuery(): SupportTimelineQueryOptions | null {
    this.validationMessage = '';

    const startUtc = this.dateInputToIso(this.timelineStartDate);
    const endUtc = this.dateInputToIso(this.timelineEndDate, true);

    if (!startUtc || !endUtc) {
      this.validationMessage = 'Timeline filters require valid start and end dates.';
      return null;
    }

    if (startUtc > endUtc) {
      this.validationMessage = 'Timeline start date must be before or equal to end date.';
      return null;
    }

    return {
      eventType: this.timelineEventType === 'all' ? null : this.timelineEventType,
      startUtc,
      endUtc,
      page: 1,
      maxItems: this.timelineMaxItems
    };
  }

  private buildPrivilegedAuditQuery(targetUserId: string): PrivilegedAuditQueryOptions | null {
    const startUtc = this.dateInputToIso(this.auditStartDate);
    const endUtc = this.dateInputToIso(this.auditEndDate, true);

    if (!startUtc || !endUtc) {
      this.validationMessage = 'Privileged audit filters require valid start and end dates.';
      return null;
    }

    if (startUtc > endUtc) {
      this.validationMessage = 'Privileged audit start date must be before or equal to end date.';
      return null;
    }

    return {
      actorUserId: this.auditActorUserId.trim() || null,
      targetUserId: targetUserId,
      actionType: this.auditActionType.trim() || null,
      startUtc,
      endUtc,
      page: 1,
      pageSize: this.auditPageSize
    };
  }

  private isGuid(value: string): boolean {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(value);
  }

  private toDateInputValue(value: Date): string {
    return value.toISOString().slice(0, 10);
  }

  private dateInputToIso(value: string, inclusiveEnd = false): string | null {
    const normalized = value.trim();
    if (!normalized) {
      return null;
    }

    const suffix = inclusiveEnd ? 'T23:59:59.999Z' : 'T00:00:00.000Z';
    const parsed = new Date(`${normalized}${suffix}`);
    if (Number.isNaN(parsed.getTime())) {
      return null;
    }

    return parsed.toISOString();
  }
}
