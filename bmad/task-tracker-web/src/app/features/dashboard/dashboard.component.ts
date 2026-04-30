import { Component, inject } from '@angular/core';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Router, RouterLink } from '@angular/router';
import {
  ProgressStreakSnapshot,
  ProgressTrendGranularity,
  ProgressTrendSummary,
  ProgressXpSummary
} from '../../shared/models/progress.models';
import { GlobalStatisticsSnapshot } from '../../shared/models/statistics.models';
import { AuthService } from '../../shared/services/auth.service';
import { ProgressService } from '../../shared/services/progress.service';
import { StatisticsService } from '../../shared/services/statistics.service';

type DashboardProgressState = 'loading' | 'ready' | 'error';
type DashboardMomentumState = 'loading' | 'ready' | 'empty' | 'error';
type DashboardStatisticsState = 'loading' | 'ready' | 'error';
type MomentumTrendDirection = 'up' | 'down' | 'steady';

interface MomentumSummaryViewModel {
  totalCompletedInWindow: number;
  recentCompletions: number;
  previousCompletions: number;
  delta: number;
  direction: MomentumTrendDirection;
  directionLabel: string;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="dashboard-page">
      <section class="panel">
        <p class="eyebrow">TaskTracker</p>
        <h1>Progress dashboard</h1>
        <p>Track XP momentum and streak continuity with server-authoritative progress data.</p>

        @if (progressState === 'loading') {
          <section class="state-card" aria-live="polite" aria-busy="true">
            <p class="state-title">Loading progress...</p>
          </section>
        } @else if (progressState === 'error') {
          <section class="state-card" role="alert" aria-live="assertive">
            <p class="state-title">{{ progressError }}</p>
            <button type="button" class="refresh-button" (click)="refreshProgress()">Retry progress refresh</button>
          </section>
        } @else {
          <section class="grid" aria-label="Progress summary cards">
            <article class="metric-card" aria-live="polite">
              <p class="metric-label">XP total</p>
              <p class="metric-value">{{ xpSummary?.totalXp ?? 0 }}</p>
              <p class="metric-meta">Ledger entries: {{ xpSummary?.ledgerEntryCount ?? 0 }}</p>
            </article>

            <article class="metric-card" aria-live="polite">
              <p class="metric-label">Streak continuity</p>
              <p class="metric-value">{{ streakSnapshot?.currentStreakDays ?? 0 }} day(s)</p>
              <p class="metric-meta">{{ streakStatusLabel() }}</p>
              <p class="metric-meta">Longest streak: {{ streakSnapshot?.longestStreakDays ?? 0 }} day(s)</p>
            </article>

            <article class="metric-card" aria-live="polite">
              <p class="metric-label">Completed in selected window</p>
              <p class="metric-value">{{ momentumSummary?.totalCompletedInWindow ?? 0 }}</p>
              <p class="metric-meta">Based on {{ selectedGranularityLabel() }} trend buckets.</p>
            </article>

            <article class="metric-card" aria-live="polite">
              <p class="metric-label">Recent trend</p>
              <p class="metric-value">{{ recentCompletionsLabel() }}</p>
              <p class="metric-meta">
                <span class="trend-indicator" [attr.data-trend]="momentumSummary?.direction ?? 'steady'">
                  <span aria-hidden="true">{{ trendDirectionIcon(momentumSummary?.direction) }}</span>
                  <span>{{ momentumSummary?.directionLabel ?? 'No trend change yet' }}</span>
                </span>
              </p>
            </article>
          </section>
        }

        <section class="global-panel" aria-labelledby="global-stats-heading">
          <div class="global-header">
            <h2 id="global-stats-heading">Global task activity</h2>
          </div>

          @if (statisticsState === 'loading') {
            <section class="state-card" aria-live="polite" aria-busy="true">
              <p class="state-title">Loading global activity...</p>
            </section>
          } @else if (statisticsState === 'error') {
            <section class="state-card" role="alert" aria-live="assertive">
              <p class="state-title">{{ statisticsError }}</p>
              <button type="button" class="refresh-button" (click)="refreshStatistics()">Retry global activity refresh</button>
            </section>
          } @else {
            <section class="grid" aria-label="Global activity summary cards">
              <article class="metric-card" aria-live="polite">
                <p class="metric-label">Total tasks created</p>
                <p class="metric-value">{{ globalStatistics?.totalTasksCreated ?? 0 }}</p>
                <p class="metric-meta">Across all active users.</p>
              </article>

              <article class="metric-card" aria-live="polite">
                <p class="metric-label">Total tasks completed</p>
                <p class="metric-value">{{ globalStatistics?.totalTasksCompleted ?? 0 }}</p>
                <p class="metric-meta">Completion rate: {{ completionRateLabel() }}</p>
              </article>
            </section>
          }
        </section>

        <section class="momentum-panel" aria-labelledby="momentum-heading">
          <div class="momentum-header">
            <h2 id="momentum-heading">Momentum summary</h2>
            <div class="momentum-controls" role="group" aria-label="Momentum view controls">
              <label>
                Granularity
                <select
                  [value]="selectedGranularity"
                  (change)="onGranularityChanged($event)"
                  [attr.aria-label]="'Trend granularity selector'"
                >
                  <option value="daily">Daily</option>
                  <option value="weekly">Weekly</option>
                </select>
              </label>

              <label>
                Window
                <select
                  [value]="selectedWindow"
                  (change)="onWindowChanged($event)"
                  [attr.aria-label]="'Trend window selector'"
                >
                  @for (windowOption of availableWindowOptions(); track windowOption) {
                    <option [value]="windowOption">{{ windowOptionLabel(windowOption) }}</option>
                  }
                </select>
              </label>
            </div>
          </div>

          @if (momentumState === 'loading') {
            <section class="state-card" aria-live="polite" aria-busy="true">
              <p class="state-title">Loading momentum history...</p>
            </section>
          } @else if (momentumState === 'error') {
            <section class="state-card" role="alert" aria-live="assertive">
              <p class="state-title">{{ momentumError }}</p>
              <button type="button" class="refresh-button" (click)="refreshMomentum()">Retry momentum refresh</button>
            </section>
          } @else if (momentumState === 'empty') {
            <section class="state-card" aria-live="polite">
              <p class="state-title">No completions found in this window yet.</p>
              <p class="metric-meta">Complete a task and refresh to start seeing trend momentum.</p>
              <button type="button" class="refresh-button" (click)="refreshMomentum()">Refresh momentum data</button>
            </section>
          } @else {
            <div class="trend-table-wrap" role="region" aria-label="Historical progress table">
              <table>
                <caption class="sr-only">Historical progress points by selected trend bucket</caption>
                <thead>
                  <tr>
                    <th scope="col">Window start</th>
                    <th scope="col">Window end</th>
                    <th scope="col">Completed tasks</th>
                    <th scope="col">XP granted</th>
                  </tr>
                </thead>
                <tbody>
                  @for (point of trendSummary?.items ?? []; track point.bucketStartUtc) {
                    <tr>
                      <td>{{ formatDateLabel(point.bucketStartUtc) }}</td>
                      <td>{{ formatDateLabel(point.bucketEndUtc) }}</td>
                      <td>{{ point.completedTaskCount }}</td>
                      <td>{{ point.xpGranted }}</td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>

        <nav class="actions" aria-label="Dashboard navigation">
          <a routerLink="/tasks" class="settings-link">View task lists</a>
          <a routerLink="/tasks/new" class="settings-link">Create a new task</a>
          <a routerLink="/leaderboards" class="settings-link">View leaderboards</a>
          <a routerLink="/account" class="settings-link">Open account settings</a>
        </nav>

        <button type="button" (click)="logout()">Log out</button>
      </section>
    </main>
  `,
  styles: [
    `
      .dashboard-page {
        min-height: 100vh;
        display: grid;
        place-items: center;
        padding: 1.5rem;
      }

      .panel {
        width: min(100%, 50rem);
        border-radius: 1rem;
        padding: 2rem;
        border: 1px solid rgba(142, 231, 255, 0.35);
        background: rgba(13, 25, 44, 0.84);
        box-shadow: 0 1.25rem 2.5rem rgba(0, 16, 31, 0.45);
      }

      .eyebrow {
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.09em;
        font-size: 0.75rem;
        color: #8ee7ff;
      }

      h1 {
        margin: 0.4rem 0;
      }

      .grid {
        margin-top: 1rem;
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 0.75rem;
      }

      .momentum-panel {
        margin-top: 1rem;
      }

      .global-panel {
        margin-top: 1rem;
      }

      .global-header {
        display: flex;
        align-items: center;
        justify-content: space-between;
      }

      .global-header h2 {
        margin: 0;
        font-size: 1.05rem;
      }

      .momentum-header {
        display: flex;
        align-items: end;
        justify-content: space-between;
        flex-wrap: wrap;
        gap: 0.75rem;
      }

      .momentum-header h2 {
        margin: 0;
        font-size: 1.05rem;
      }

      .momentum-controls {
        display: flex;
        flex-wrap: wrap;
        gap: 0.6rem;
      }

      .momentum-controls label {
        display: grid;
        gap: 0.35rem;
        color: #c2d8e7;
        font-size: 0.85rem;
      }

      .momentum-controls select {
        border-radius: 0.55rem;
        border: 1px solid #4f7da0;
        padding: 0.4rem 0.5rem;
        background: #0f1d31;
        color: #ecf6ff;
      }

      .metric-card,
      .state-card {
        border-radius: 0.75rem;
        border: 1px solid #355d79;
        background: #0f1d31;
        padding: 0.85rem;
      }

      .metric-label {
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        font-size: 0.73rem;
        color: #9fd7ff;
      }

      .metric-value {
        margin: 0.35rem 0 0;
        font-size: 1.35rem;
        font-weight: 700;
      }

      .metric-meta,
      .state-title {
        margin: 0.35rem 0 0;
        color: #c2d8e7;
      }

      .trend-indicator {
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        font-weight: 600;
      }

      .trend-indicator[data-trend='up'] {
        color: #9ff4cb;
      }

      .trend-indicator[data-trend='down'] {
        color: #ffbd98;
      }

      .trend-indicator[data-trend='steady'] {
        color: #ffe8a3;
      }

      .trend-table-wrap {
        margin-top: 0.85rem;
        border-radius: 0.75rem;
        border: 1px solid #355d79;
        background: #0f1d31;
        overflow-x: auto;
      }

      table {
        width: 100%;
        border-collapse: collapse;
      }

      th,
      td {
        text-align: left;
        padding: 0.65rem 0.75rem;
        border-bottom: 1px solid rgba(88, 128, 163, 0.45);
        white-space: nowrap;
      }

      th {
        color: #9fd7ff;
        font-size: 0.8rem;
        text-transform: uppercase;
        letter-spacing: 0.04em;
      }

      .sr-only {
        position: absolute;
        width: 1px;
        height: 1px;
        padding: 0;
        margin: -1px;
        overflow: hidden;
        clip: rect(0, 0, 0, 0);
        white-space: nowrap;
        border: 0;
      }

      .refresh-button {
        margin-top: 0.7rem;
        border-radius: 0.65rem;
        border: 1px solid #7fd0ff;
        background: #143450;
        color: #ecf6ff;
        font-weight: 600;
        padding: 0.5rem 0.75rem;
      }

      .actions {
        margin-top: 1rem;
        display: grid;
        gap: 0.6rem;
      }

      button {
        margin-top: 1rem;
        border: 0;
        border-radius: 0.65rem;
        padding: 0.8rem 1.1rem;
        color: #0a2238;
        font-weight: 700;
        background: linear-gradient(120deg, #8ee7ff, #b8f8f2);
      }

      .metric-card,
      .trend-table-wrap {
        animation: card-enter 240ms ease-out both;
      }

      .settings-link {
        display: block;
        color: #b8f8f2;
        text-decoration: none;
      }

      .settings-link:hover,
      .settings-link:focus-visible {
        text-decoration: underline;
      }

      @media (max-width: 768px) {
        .panel {
          padding: 1.2rem;
        }

        .grid {
          grid-template-columns: 1fr;
        }

        .momentum-controls {
          width: 100%;
        }

        .momentum-controls label {
          flex: 1;
          min-width: 10.5rem;
        }
      }

      @media (prefers-reduced-motion: reduce) {
        .metric-card,
        .trend-table-wrap {
          animation: none;
        }
      }

      @keyframes card-enter {
        from {
          opacity: 0;
          transform: translateY(6px);
        }

        to {
          opacity: 1;
          transform: translateY(0);
        }
      }
    `
  ]
})
export class DashboardComponent {
  private readonly authService = inject(AuthService);
  private readonly progressService = inject(ProgressService);
  private readonly statisticsService = inject(StatisticsService);
  private readonly router = inject(Router);
  xpSummary: ProgressXpSummary | null = null;
  streakSnapshot: ProgressStreakSnapshot | null = null;
  trendSummary: ProgressTrendSummary | null = null;
  globalStatistics: GlobalStatisticsSnapshot | null = null;
  momentumSummary: MomentumSummaryViewModel | null = null;
  progressState: DashboardProgressState = 'loading';
  momentumState: DashboardMomentumState = 'loading';
  statisticsState: DashboardStatisticsState = 'loading';
  progressError = '';
  momentumError = '';
  statisticsError = '';
  selectedGranularity: ProgressTrendGranularity = 'daily';
  selectedWindow = 30;
  private trendRequestVersion = 0;
  readonly dailyWindowOptions = [14, 30, 60];
  readonly weeklyWindowOptions = [4, 8, 12];

  constructor() {
    this.refreshProgress();
    this.refreshStatistics();
    this.refreshMomentum();
  }

  async logout(): Promise<void> {
    this.authService.logout().subscribe({
      next: async () => {
        await this.router.navigate(['/login']);
      },
      error: async () => {
        // Tokens are cleared locally even on server error; redirect to login.
        await this.router.navigate(['/login']);
      }
    });
  }

  refreshProgress(): void {
    this.progressState = 'loading';
    this.progressError = '';

    forkJoin({
      xpSummary: this.progressService.getXpSummary().pipe(catchError(() => of(null))),
      streakSnapshot: this.progressService.getStreakSnapshot().pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ xpSummary, streakSnapshot }) => {
        if (xpSummary) {
          this.xpSummary = xpSummary;
        }

        if (streakSnapshot) {
          this.streakSnapshot = streakSnapshot;
        }

        if (this.xpSummary || this.streakSnapshot) {
          this.progressState = 'ready';
          return;
        }

        this.progressState = 'error';
        this.progressError = 'Unable to load progress right now. Try again in a moment.';
      },
      error: () => {
        this.progressState = 'error';
        this.progressError = 'Unable to load progress right now. Try again in a moment.';
      }
    });
  }

  refreshMomentum(): void {
    const activeRequestVersion = ++this.trendRequestVersion;

    this.momentumState = 'loading';
    this.momentumError = '';

    this.progressService.getTrendSummary(this.selectedGranularity, this.selectedWindowDays()).subscribe({
      next: (trendSummary) => {
        if (activeRequestVersion !== this.trendRequestVersion) {
          return;
        }

        this.trendSummary = trendSummary;
        this.momentumSummary = this.buildMomentumSummary(trendSummary);

        if (!trendSummary.items.length || this.momentumSummary.totalCompletedInWindow === 0) {
          this.momentumState = 'empty';
          return;
        }

        this.momentumState = 'ready';
      },
      error: () => {
        if (activeRequestVersion !== this.trendRequestVersion) {
          return;
        }

        this.momentumState = 'error';
        this.momentumError = 'Unable to load momentum history right now. Try again in a moment.';
      }
    });
  }

  refreshStatistics(): void {
    this.statisticsState = 'loading';
    this.statisticsError = '';

    this.statisticsService.getGlobalStatistics().subscribe({
      next: (snapshot) => {
        this.globalStatistics = snapshot;
        this.statisticsState = 'ready';
      },
      error: () => {
        this.statisticsState = 'error';
        this.statisticsError = 'Unable to load global task activity right now. Try again in a moment.';
      }
    });
  }

  onGranularityChanged(event: Event): void {
    const granularity = (event.target as HTMLSelectElement).value as ProgressTrendGranularity;
    if (granularity !== 'daily' && granularity !== 'weekly') {
      return;
    }

    this.selectedGranularity = granularity;
    this.selectedWindow = granularity === 'daily' ? 30 : 12;
    this.refreshMomentum();
  }

  onWindowChanged(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    if (Number.isNaN(value)) {
      return;
    }

    this.selectedWindow = value;
    this.refreshMomentum();
  }

  availableWindowOptions(): number[] {
    return this.selectedGranularity === 'daily' ? this.dailyWindowOptions : this.weeklyWindowOptions;
  }

  windowOptionLabel(option: number): string {
    return this.selectedGranularity === 'daily' ? `${option} days` : `${option} weeks`;
  }

  selectedGranularityLabel(): string {
    return this.selectedGranularity === 'daily' ? 'daily' : 'weekly';
  }

  recentCompletionsLabel(): string {
    if (!this.momentumSummary) {
      return 'No recent history yet';
    }

    return `${this.momentumSummary.recentCompletions} (previous window ${this.momentumSummary.previousCompletions})`;
  }

  trendDirectionIcon(direction: MomentumTrendDirection | undefined): string {
    if (direction === 'up') {
      return '^';
    }

    if (direction === 'down') {
      return 'v';
    }

    return '=';
  }

  formatDateLabel(utcIsoDateTime: string): string {
    return utcIsoDateTime.slice(0, 10);
  }

  streakStatusLabel(): string {
    if (!this.streakSnapshot) {
      return 'No streak data yet';
    }

    if (this.streakSnapshot.outcome === 'continue') {
      return 'Continuity maintained';
    }

    if (this.streakSnapshot.outcome === 'restart') {
      return 'Continuity restarted';
    }

    return 'Continuity reset';
  }

  completionRateLabel(): string {
    if (!this.globalStatistics || this.globalStatistics.totalTasksCreated === 0) {
      return '0%';
    }

    const rate = (this.globalStatistics.totalTasksCompleted / this.globalStatistics.totalTasksCreated) * 100;
    return `${Math.round(rate)}%`;
  }

  private selectedWindowDays(): number {
    return this.selectedGranularity === 'daily' ? this.selectedWindow : this.selectedWindow * 7;
  }

  private buildMomentumSummary(trendSummary: ProgressTrendSummary): MomentumSummaryViewModel {
    const completedCounts = trendSummary.items.map((item) => item.completedTaskCount);
    const totalCompletedInWindow = completedCounts.reduce((sum, count) => sum + count, 0);

    const comparisonBucketCount = this.selectedGranularity === 'daily' ? 7 : 1;
    const splitSize = Math.min(comparisonBucketCount, completedCounts.length);
    const recentCompletions = completedCounts.slice(-splitSize).reduce((sum, count) => sum + count, 0);
    const previousCompletions = completedCounts
      .slice(Math.max(0, completedCounts.length - splitSize * 2), Math.max(0, completedCounts.length - splitSize))
      .reduce((sum, count) => sum + count, 0);

    const delta = recentCompletions - previousCompletions;
    const direction: MomentumTrendDirection = delta > 0 ? 'up' : delta < 0 ? 'down' : 'steady';

    return {
      totalCompletedInWindow,
      recentCompletions,
      previousCompletions,
      delta,
      direction,
      directionLabel: this.directionLabel(direction, delta)
    };
  }

  private directionLabel(direction: MomentumTrendDirection, delta: number): string {
    if (direction === 'up') {
      return `Up by ${delta} completions`;
    }

    if (direction === 'down') {
      return `Down by ${Math.abs(delta)} completions`;
    }

    return 'No change from previous window';
  }
}