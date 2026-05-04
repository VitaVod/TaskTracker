import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs/operators';
import {
  ModerationActionType,
  SuspiciousAnomalyType,
  SuspiciousCaseItem,
  SuspiciousCasesProblemDetails
} from '../../shared/models/suspicious-cases.models';
import { SuspiciousCasesService } from '../../shared/services/suspicious-cases.service';

type SuspiciousCasesViewState = 'loading' | 'ready' | 'empty' | 'error';

interface SuspiciousCaseFilterOption {
  value: SuspiciousAnomalyType | 'all';
  label: string;
  ariaLabel: string;
}

interface ModerationReasonOption {
  code: string;
  label: string;
}

@Component({
  selector: 'app-ops-suspicious-cases',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './ops-suspicious-cases.component.html',
  styleUrl: './ops-suspicious-cases.component.scss'
})
export class OpsSuspiciousCasesComponent {
  private readonly suspiciousCasesService = inject(SuspiciousCasesService);

  readonly pageSize = 20;
  readonly reasonOptions: ReadonlyArray<ModerationReasonOption> = [
    { code: 'suspicious-ranking-signal', label: 'Suspicious ranking signal' },
    { code: 'manual-investigation-confirmed', label: 'Manual investigation confirmed' },
    { code: 'abuse-prevention-policy', label: 'Abuse prevention policy' }
  ];
  readonly filterOptions: ReadonlyArray<SuspiciousCaseFilterOption> = [
    { value: 'all', label: 'All anomalies', ariaLabel: 'Show all suspicious cases' },
    { value: 'rankingMismatch', label: 'Ranking mismatch', ariaLabel: 'Show ranking mismatch anomalies' },
    { value: 'activitySpike', label: 'Activity spike', ariaLabel: 'Show activity spike anomalies' }
  ];

  selectedFilter: SuspiciousAnomalyType | 'all' = 'all';
  page = 1;
  hasNextPage = false;
  totalCount = 0;
  state: SuspiciousCasesViewState = 'loading';
  cases: SuspiciousCaseItem[] = [];
  errorMessage = '';
  errorSupportText = '';
  liveMessage = '';
  selectedReasonCode = this.reasonOptions[0]?.code ?? '';
  reasonText = '';

  isSubmittingAction = false;
  pendingCaseId: string | null = null;
  actionFeedback = '';
  actionError = '';

  confirmationCase: SuspiciousCaseItem | null = null;
  confirmationChecked = false;

  constructor() {
    this.loadCases(false);
  }

  selectFilter(filter: SuspiciousAnomalyType | 'all'): void {
    if (filter === this.selectedFilter) {
      return;
    }

    this.selectedFilter = filter;
    this.page = 1;
    this.loadCases(true);
  }

  setFilterFromKeyboard(event: Event, filter: SuspiciousAnomalyType | 'all'): void {
    event.preventDefault();
    this.selectFilter(filter);
  }

  retry(): void {
    this.loadCases(true);
  }

  previousPage(): void {
    if (this.page <= 1 || this.state === 'loading') {
      return;
    }

    this.page -= 1;
    this.loadCases(true);
  }

  nextPage(): void {
    if (!this.hasNextPage || this.state === 'loading') {
      return;
    }

    this.page += 1;
    this.loadCases(true);
  }

  trackByCaseId(_index: number, item: SuspiciousCaseItem): string {
    return item.caseId;
  }

  isCasePending(item: SuspiciousCaseItem): boolean {
    return this.isSubmittingAction && this.pendingCaseId === item.caseId;
  }

  canSubmitNonDestructiveAction(item: SuspiciousCaseItem): boolean {
    return !this.isCasePending(item) && !this.isSubmittingAction && this.selectedReasonCode.trim().length > 0;
  }

  queueRankingCorrection(item: SuspiciousCaseItem): void {
    this.actionFeedback = '';
    this.actionError = '';
    this.confirmationChecked = false;
    this.confirmationCase = item;
  }

  closeConfirmationDialog(): void {
    if (this.isSubmittingAction) {
      return;
    }

    this.confirmationChecked = false;
    this.confirmationCase = null;
  }

  setReasonCode(event: Event): void {
    const target = event.target as HTMLSelectElement | null;
    this.selectedReasonCode = target?.value ?? '';
  }

  setReasonText(event: Event): void {
    const target = event.target as HTMLTextAreaElement | null;
    this.reasonText = target?.value ?? '';
  }

  setConfirmationChecked(event: Event): void {
    const target = event.target as HTMLInputElement | null;
    this.confirmationChecked = Boolean(target?.checked);
  }

  submitFlagEntity(item: SuspiciousCaseItem): void {
    if (!this.canSubmitNonDestructiveAction(item)) {
      return;
    }

    this.submitModerationAction(item, 'flagEntity', false);
  }

  confirmRankingCorrection(): void {
    if (!this.confirmationCase || !this.confirmationChecked || this.isSubmittingAction) {
      return;
    }

    this.submitModerationAction(this.confirmationCase, 'rankingCorrection', true);
  }

  severityLabel(item: SuspiciousCaseItem): string {
    if (item.severity >= 85) {
      return 'critical';
    }

    if (item.severity >= 70) {
      return 'high';
    }

    if (item.severity >= 50) {
      return 'medium';
    }

    return 'low';
  }

  anomalyLabel(item: SuspiciousCaseItem): string {
    return item.anomalyType === 'activitySpike' ? 'Activity spike' : 'Ranking mismatch';
  }

  paginationSummary(): string {
    if (this.totalCount === 0) {
      return '0 cases';
    }

    const firstItem = (this.page - 1) * this.pageSize + 1;
    const lastItem = Math.min(this.page * this.pageSize, this.totalCount);
    return `${firstItem}-${lastItem} of ${this.totalCount}`;
  }

  formatTimestamp(value: string | null): string {
    if (!value) {
      return 'No activity timestamp';
    }

    const parsed = new Date(value);
    if (Number.isNaN(parsed.getTime())) {
      return value;
    }

    return parsed.toLocaleString();
  }

  private loadCases(announce: boolean): void {
    this.state = 'loading';
    this.errorMessage = '';
    this.errorSupportText = '';

    this.suspiciousCasesService.getCases(this.selectedFilter, this.page, this.pageSize).subscribe({
      next: (response) => {
        this.page = response.page;
        this.hasNextPage = response.hasNextPage;
        this.totalCount = response.totalCount;
        this.cases = response.items;
        this.state = response.items.length === 0 ? 'empty' : 'ready';

        if (announce) {
          this.liveMessage = `Suspicious case list updated. Page ${this.page}.`;
        }
      },
      error: (problem: SuspiciousCasesProblemDetails) => {
        this.state = 'error';
        this.errorMessage = 'Unable to load suspicious cases right now. Try again in a moment.';
        const supportParts = [problem.code, problem.traceId].filter((value): value is string => Boolean(value));
        this.errorSupportText = supportParts.length > 0 ? `Support: ${supportParts.join(' | ')}` : '';
      }
    });
  }

  private submitModerationAction(item: SuspiciousCaseItem, actionType: ModerationActionType, confirmDestructive: boolean): void {
    this.isSubmittingAction = true;
    this.pendingCaseId = item.caseId;
    this.actionFeedback = '';
    this.actionError = '';

    this.suspiciousCasesService
      .applyModerationAction(item.caseId, {
        actionType,
        reasonCode: this.selectedReasonCode.trim(),
        reasonText: this.reasonText.trim(),
        confirmDestructive,
        confirmationToken: confirmDestructive ? item.destructiveConfirmationToken : null
      })
      .pipe(
        finalize(() => {
          this.isSubmittingAction = false;
          this.pendingCaseId = null;
        })
      )
      .subscribe({
        next: (response) => {
          this.actionFeedback = response.outcome === 'alreadyApplied'
            ? `Moderation intent already applied for ${item.caseId}.`
            : `${actionType === 'rankingCorrection' ? 'Ranking correction' : 'Entity flag'} applied for ${item.caseId}.`;

          this.liveMessage = this.actionFeedback;
          this.confirmationCase = null;
          this.confirmationChecked = false;
          this.loadCases(false);
        },
        error: (problem: SuspiciousCasesProblemDetails) => {
          const supportParts = [problem.code, problem.traceId].filter((value): value is string => Boolean(value));
          const supportText = supportParts.length > 0 ? ` (${supportParts.join(' | ')})` : '';
          this.actionError = `${problem.detail ?? 'Moderation action failed.'}${supportText}`;
        }
      });
  }
}
