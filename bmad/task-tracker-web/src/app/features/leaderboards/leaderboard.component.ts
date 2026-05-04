import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LeaderboardEntry, LeaderboardProblemDetails, LeaderboardType } from '../../shared/models/leaderboard.models';
import { AuthService } from '../../shared/services/auth.service';
import { LeaderboardService } from '../../shared/services/leaderboard.service';

type LeaderboardViewState = 'loading' | 'ready' | 'empty' | 'error';

interface LeaderboardTypeOption {
  value: LeaderboardType;
  label: string;
  ariaLabel: string;
}

@Component({
  selector: 'app-leaderboard',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './leaderboard.component.html',
  styleUrl: './leaderboard.component.scss'
})
export class LeaderboardComponent {
  private readonly leaderboardService = inject(LeaderboardService);
  private readonly authService = inject(AuthService);

  readonly typeOptions: ReadonlyArray<LeaderboardTypeOption> = [
    {
      value: 'streak',
      label: 'Streak',
      ariaLabel: 'Show streak leaderboard'
    },
    {
      value: 'completedTasks',
      label: 'Completed tasks',
      ariaLabel: 'Show completed tasks leaderboard'
    }
  ];

  readonly pageSize = 20;
  selectedType: LeaderboardType = 'streak';
  page = 1;
  totalCount = 0;
  hasNextPage = false;
  entries: LeaderboardEntry[] = [];
  state: LeaderboardViewState = 'loading';
  errorMessage = '';
  errorSupportText = '';
  liveMessage = '';
  readonly isAdmin = this.authService.hasRole('admin');

  constructor() {
    this.loadLeaderboard(false);
  }

  selectType(type: LeaderboardType): void {
    if (type === this.selectedType) {
      return;
    }

    this.selectedType = type;
    this.page = 1;
    this.loadLeaderboard(true);
  }

  setTypeFromKeyboard(event: Event, type: LeaderboardType): void {
    event.preventDefault();
    this.selectType(type);
  }

  retry(): void {
    this.loadLeaderboard(true);
  }

  previousPage(): void {
    if (this.page <= 1 || this.state === 'loading') {
      return;
    }

    this.page -= 1;
    this.loadLeaderboard(true);
  }

  nextPage(): void {
    if (!this.hasNextPage || this.state === 'loading') {
      return;
    }

    this.page += 1;
    this.loadLeaderboard(true);
  }

  trackByRank(_index: number, entry: LeaderboardEntry): string {
    return `${entry.rank}-${entry.publicIdentity}`;
  }

  typeHeading(): string {
    return this.selectedType === 'streak' ? 'Streak leaderboard' : 'Completed tasks leaderboard';
  }

  metricColumnHeading(): string {
    return this.selectedType === 'streak' ? 'Streak days' : 'Tasks completed';
  }

  metricValueLabel(entry: LeaderboardEntry): string {
    return this.selectedType === 'streak' ? `${entry.metricValue} day streak` : `${entry.metricValue} completed task(s)`;
  }

  identityModeLabel(entry: LeaderboardEntry): string {
    return entry.identityMode === 'anonymous' ? 'Anonymous participant' : 'Public participant';
  }

  canViewPublicProfile(entry: LeaderboardEntry): boolean {
    return entry.identityMode === 'public' && !!entry.publicProfileHandle;
  }

  publicProfileRoute(entry: LeaderboardEntry): string[] {
    return ['/profile/public', entry.publicProfileHandle ?? ''];
  }

  movementSymbol(): string {
    return '=';
  }

  movementText(): string {
    return 'No movement data';
  }

  movementAriaLabel(entry: LeaderboardEntry): string {
    return `Movement for rank ${entry.rank}: no movement data available`;
  }

  paginationSummary(): string {
    if (this.totalCount === 0) {
      return '0 total participants';
    }

    const firstItem = (this.page - 1) * this.pageSize + 1;
    const lastItem = Math.min(this.page * this.pageSize, this.totalCount);
    return `${firstItem}-${lastItem} of ${this.totalCount}`;
  }

  private loadLeaderboard(announce: boolean): void {
    this.state = 'loading';
    this.errorMessage = '';
    this.errorSupportText = '';

    this.leaderboardService.getLeaderboard(this.selectedType, this.page, this.pageSize).subscribe({
      next: (response) => {
        this.page = response.page;
        this.totalCount = response.totalCount;
        this.hasNextPage = response.hasNextPage;
        this.entries = response.items;
        this.state = response.items.length === 0 ? 'empty' : 'ready';

        if (announce) {
          this.liveMessage = `${this.typeHeading()} updated. Page ${this.page}.`;
        }
      },
      error: (problem: LeaderboardProblemDetails) => {
        this.state = 'error';
        this.errorMessage = 'Unable to load leaderboard right now. Try again in a moment.';

        const supportParts = [problem.code, problem.traceId].filter((value): value is string => Boolean(value));
        this.errorSupportText = supportParts.length > 0 ? `Support: ${supportParts.join(' | ')}` : '';
      }
    });
  }
}
