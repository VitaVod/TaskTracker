import { AfterViewInit, Component, OnDestroy, inject } from '@angular/core';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { NavigationEnd, Router, RouterLink } from '@angular/router';
import {
  ProgressLevelSnapshot,
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
type XpBandState = 'reached' | 'active' | 'locked';

interface MomentumSummaryViewModel {
  totalCompletedInWindow: number;
  recentCompletions: number;
  previousCompletions: number;
  delta: number;
  direction: MomentumTrendDirection;
  directionLabel: string;
}

interface RecoveryPromptViewModel {
  impactLabel: string;
  impactMessage: string;
  actionLabel: string;
  actionRoute: string;
  announcement: string;
}

interface ProgressErrorWithCode {
  code?: string;
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
              @if (xpSummary?.outcomeExplanation; as xpExplanation) {
                <p class="metric-meta outcome-explanation" [attr.data-reason-code]="xpExplanation.reasonCode">
                  {{ xpExplanation.message }}
                </p>
              }
              @if (xpSummary?.levelProgress; as levelProgress) {
                <section class="xp-level-card" aria-label="XP level progress">
                  <p class="metric-meta">
                    Level {{ levelProgress.currentLevel }}
                    <span class="xp-level-separator" aria-hidden="true">/</span>
                    Next level {{ levelProgress.nextLevel }} at {{ levelProgress.nextLevelThresholdXp }} XP
                  </p>
                  <p class="metric-meta">
                    {{ levelProgress.percentToNextLevel }}% to next level
                    ({{ xpRemainingToNextLevel(levelProgress) }} XP remaining)
                  </p>

                  <div class="xp-progress-track" role="progressbar"
                    [attr.aria-valuemin]="0"
                    [attr.aria-valuemax]="100"
                    [attr.aria-valuenow]="levelProgress.percentToNextLevel"
                    [attr.aria-label]="xpProgressAriaLabel(levelProgress)">
                    <div class="xp-progress-fill" [style.width.%]="levelProgress.percentToNextLevel"></div>
                  </div>

                  <p class="metric-meta xp-band-summary">{{ xpBandSummary(levelProgress) }}</p>

                  <ul class="xp-band-list" aria-label="XP milestone bands">
                    @for (bandLevel of levelProgress.bandMilestoneLevels; track bandLevel) {
                      <li class="xp-band-item" [attr.data-state]="xpBandState(levelProgress, bandLevel)">
                        <span class="xp-band-icon" aria-hidden="true">{{ xpBandIcon(levelProgress, bandLevel) }}</span>
                        <span>Level {{ bandLevel }}</span>
                        <span class="sr-only">{{ xpBandAssistiveLabel(levelProgress, bandLevel) }}</span>
                      </li>
                    }
                  </ul>
                </section>
              }
            </article>

            <article class="metric-card" aria-live="polite">
              <p class="metric-label">Streak continuity</p>
              <p class="metric-value">{{ streakSnapshot?.currentStreakDays ?? 0 }} day(s)</p>
              <p class="metric-meta">{{ streakStatusLabel() }}</p>
              <p class="metric-meta">Longest streak: {{ streakSnapshot?.longestStreakDays ?? 0 }} day(s)</p>
              @if (streakSnapshot?.outcomeExplanation; as streakExplanation) {
                <p class="metric-meta outcome-explanation" [attr.data-reason-code]="streakExplanation.reasonCode">
                  {{ streakExplanation.message }}
                </p>
              }
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

          @if (recoveryPrompt(); as recovery) {
            <section class="recovery-card" role="region" aria-live="polite" aria-labelledby="recovery-heading">
              <p class="metric-label" id="recovery-heading">Recovery prompt</p>
              <p class="recovery-impact">{{ recovery.impactLabel }}</p>
              <p class="metric-meta">{{ recovery.impactMessage }}</p>
              <a class="recovery-action" [routerLink]="recovery.actionRoute">{{ recovery.actionLabel }}</a>
              <p class="sr-only">{{ recovery.announcement }}</p>
            </section>
          }
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

        <section class="momentum-panel" aria-labelledby="momentum-heading" id="momentum-section">
          <div class="momentum-header">
            <h2 id="momentum-heading">Monthly activity heatmap</h2>
          </div>

          @if (heatmapState === 'loading') {
            <section class="state-card" aria-live="polite" aria-busy="true">
              <p class="state-title">Loading monthly heatmap...</p>
            </section>
          } @else if (heatmapState === 'error') {
            <section class="state-card" role="alert" aria-live="assertive">
              <p class="state-title">{{ heatmapError }}</p>
              <button type="button" class="refresh-button" (click)="refreshHeatmap()">Retry heatmap refresh</button>
            </section>
          } @else if (heatmapState === 'empty') {
            <section class="state-card" aria-live="polite">
              <p class="state-title">No daily activity available for heatmap yet.</p>
            </section>
          } @else {
            <section class="heatmap-panel" role="region" aria-labelledby="heatmap-heading">
              <div class="heatmap-toolbar">
                <h3 id="heatmap-heading">Monthly activity heatmap</h3>
                <div class="heatmap-month-nav" aria-label="Heatmap month navigation">
                  <button
                    type="button"
                    class="month-arrow"
                    [disabled]="!canShowPreviousMonth()"
                    (click)="showPreviousHeatmapMonth()"
                    aria-label="Show previous month">
                    <span aria-hidden="true">&lt;</span>
                  </button>
                  <p class="month-label">{{ heatmapMonthLabel() }}</p>
                  <button
                    type="button"
                    class="month-arrow"
                    [disabled]="!canShowNextMonth()"
                    (click)="showNextHeatmapMonth()"
                    aria-label="Show next month">
                    <span aria-hidden="true">&gt;</span>
                  </button>
                </div>
              </div>

              <p class="heatmap-caption">Keyboard: use arrow keys to move between days, then press Enter.</p>

              @if (heatmapVisibleItems().length === 0) {
                <p class="heatmap-empty">No daily activity available.</p>
              } @else {
                <div class="heatmap-grid" role="grid" aria-label="Task activity heatmap by day">
                  @for (item of heatmapVisibleItems(); track item.bucketStartUtc; let index = $index) {
                    <button
                      type="button"
                      class="heatmap-cell"
                      role="gridcell"
                      [attr.data-intensity]="heatmapIntensityLevel(item.completedTaskCount, heatmapMaxCompletedCount())"
                      [attr.data-index]="index"
                      [attr.aria-label]="heatmapCellAriaLabel(item.bucketStartUtc, item.completedTaskCount, item.xpGranted)"
                      (click)="openDayDetail(item.bucketStartUtc)"
                      (keydown)="onHeatmapCellKeydown($event, index)"
                    >
                      <span class="heatmap-cell-date">{{ heatmapDateLabel(item.bucketStartUtc) }}</span>
                      <span class="heatmap-cell-count">{{ item.completedTaskCount }}</span>
                    </button>
                  }
                </div>
              }
            </section>
          }

          <div class="momentum-summary-intro">
            <h3 id="momentum-summary-heading">Momentum Summary</h3>
          </div>

          <section class="momentum-summary-panel" role="region" aria-labelledby="momentum-summary-heading">
            <div class="momentum-header">
              <div class="momentum-summary-copy">
                <h4 class="momentum-summary-panel-title">Momentum Overview</h4>
                <p class="momentum-summary-subtitle">Review recent completion trends and XP movement in your selected window.</p>
              </div>
              <div class="momentum-controls" role="group" aria-label="Momentum summary controls">
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
              <ul class="trend-list" aria-label="Historical progress points">
                @for (point of trendSummary?.items ?? []; track point.bucketStartUtc) {
                  <li class="trend-card-item">
                    <button
                      type="button"
                      class="trend-card"
                      (click)="openDayDetail(point.bucketStartUtc)"
                      (keydown)="onTrendItemKeydown($event, point.bucketStartUtc)"
                      [attr.aria-label]="trendCardAriaLabel(point.bucketStartUtc, point.completedTaskCount, point.xpGranted)"
                    >
                      <span class="trend-card-dates">
                        <span>{{ formatDateLabel(point.bucketStartUtc) }}</span>
                        <span aria-hidden="true">-></span>
                        <span>{{ formatDateLabel(point.bucketEndUtc) }}</span>
                      </span>
                      <span class="trend-card-metrics">
                        <span>Completed {{ point.completedTaskCount }}</span>
                        <span>XP {{ point.xpGranted }}</span>
                      </span>
                    </button>
                  </li>
                }
              </ul>
            }
          </section>
        </section>

        <nav class="actions" aria-label="Dashboard navigation">
          <a routerLink="/tasks" class="settings-link">View task lists</a>
          <a routerLink="/tasks/new" class="settings-link">Create a new task</a>
          <a routerLink="/leaderboards" class="settings-link">View leaderboards</a>
          @if (isSupport) {
            <a routerLink="/ops/support/diagnostics" class="settings-link">Open support diagnostics</a>
          }
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

      .momentum-header h2,
      .momentum-header h3 {
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

      .recovery-card {
        margin-top: 0.75rem;
        border-radius: 0.75rem;
        border: 1px solid #5c8f73;
        background: linear-gradient(135deg, rgba(16, 52, 67, 0.9), rgba(12, 46, 35, 0.9));
        padding: 0.95rem;
      }

      .recovery-impact {
        margin: 0.35rem 0 0;
        font-size: 1.05rem;
        font-weight: 700;
        color: #baf5d8;
      }

      .recovery-action {
        display: inline-block;
        margin-top: 0.65rem;
        border-radius: 0.65rem;
        border: 1px solid #9ee9c3;
        color: #dfffee;
        background: rgba(12, 59, 42, 0.8);
        text-decoration: none;
        font-weight: 700;
        padding: 0.45rem 0.75rem;
      }

      .recovery-action:hover,
      .recovery-action:focus-visible {
        text-decoration: underline;
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

      .outcome-explanation {
        border-left: 2px solid rgba(159, 215, 255, 0.55);
        padding-left: 0.5rem;
      }

      .xp-level-card {
        margin-top: 0.7rem;
        border-radius: 0.65rem;
        border: 1px solid rgba(122, 198, 224, 0.45);
        background: rgba(9, 28, 43, 0.65);
        padding: 0.6rem;
      }

      .xp-level-separator {
        margin: 0 0.35rem;
        opacity: 0.65;
      }

      .xp-progress-track {
        margin-top: 0.55rem;
        width: 100%;
        height: 0.7rem;
        border-radius: 999px;
        background: rgba(63, 103, 128, 0.65);
        overflow: hidden;
      }

      .xp-progress-fill {
        height: 100%;
        border-radius: inherit;
        background: linear-gradient(90deg, #89f0ff, #74d4ff 40%, #7bf2c9 100%);
      }

      .xp-band-summary {
        margin-top: 0.55rem;
      }

      .xp-band-list {
        list-style: none;
        padding: 0;
        margin: 0.55rem 0 0;
        display: grid;
        grid-template-columns: repeat(3, minmax(0, 1fr));
        gap: 0.35rem;
      }

      .xp-band-item {
        border-radius: 0.5rem;
        border: 1px solid rgba(101, 148, 179, 0.45);
        padding: 0.35rem 0.45rem;
        display: inline-flex;
        align-items: center;
        gap: 0.35rem;
        font-size: 0.78rem;
      }

      .xp-band-item[data-state='reached'] {
        border-color: rgba(131, 237, 186, 0.65);
        background: rgba(19, 65, 46, 0.65);
      }

      .xp-band-item[data-state='active'] {
        border-color: rgba(138, 217, 255, 0.75);
        background: rgba(18, 59, 87, 0.7);
      }

      .xp-band-item[data-state='locked'] {
        border-color: rgba(101, 148, 179, 0.35);
        background: rgba(13, 33, 49, 0.72);
      }

      .xp-band-icon {
        font-weight: 700;
        width: 1rem;
        text-align: center;
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

      .trend-layout {
        margin-top: 0.85rem;
        display: grid;
        gap: 0.75rem;
        grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr);
      }

      .momentum-summary-panel {
        margin-top: 0.8rem;
        border-radius: 0.75rem;
        border: 1px solid #355d79;
        background: #0f1d31;
        padding: 0.8rem;
      }

      .momentum-summary-intro {
        margin-top: 0.8rem;
      }

      .momentum-summary-intro h3 {
        margin: 0;
        font-size: 1.05rem;
      }

      .momentum-summary-subtitle {
        margin: 0.35rem 0 0;
        color: #c2d8e7;
        font-size: 0.88rem;
      }

      .momentum-summary-panel .momentum-summary-subtitle {
        margin: 0;
      }

      .momentum-summary-panel-title {
        margin: 0 0 0.2rem;
        font-size: 0.95rem;
        color: #e6f5ff;
      }

      .momentum-summary-copy {
        min-width: 15rem;
      }

      .momentum-summary-panel .momentum-header {
        margin-bottom: 0.75rem;
        align-items: flex-start;
        justify-content: space-between;
      }

      .heatmap-panel {
        margin-top: 0.85rem;
        border-radius: 0.75rem;
        border: 1px solid #355d79;
        background: #0f1d31;
        padding: 0.8rem;
      }

      .heatmap-toolbar {
        display: flex;
        align-items: center;
        justify-content: space-between;
        gap: 0.6rem;
        flex-wrap: wrap;
      }

      .heatmap-month-nav {
        display: inline-flex;
        align-items: center;
        gap: 0.5rem;
      }

      .month-label {
        margin: 0;
        min-width: 7.5rem;
        text-align: center;
        font-size: 0.83rem;
        color: #cbe0f1;
      }

      .month-arrow {
        margin-top: 0;
        border: 1px solid #4f7da0;
        border-radius: 0.5rem;
        background: #12304b;
        color: #ecf6ff;
        padding: 0.35rem 0.55rem;
        line-height: 1;
      }

      .month-arrow[disabled] {
        opacity: 0.45;
      }

      .heatmap-panel h3 {
        margin: 0;
        font-size: 0.95rem;
      }

      .heatmap-caption {
        margin: 0.35rem 0 0;
        color: #c2d8e7;
        font-size: 0.8rem;
      }

      .heatmap-empty {
        margin: 0.7rem 0 0;
        color: #c2d8e7;
      }

      .heatmap-grid {
        margin-top: 0.7rem;
        display: grid;
        grid-template-columns: repeat(7, minmax(0, 1fr));
        gap: 0.45rem;
      }

      .heatmap-cell {
        border: 1px solid #355d79;
        border-radius: 0.55rem;
        background: #15273d;
        color: #ecf6ff;
        min-height: 3.1rem;
        padding: 0.3rem;
        display: grid;
        align-content: space-between;
        text-align: left;
      }

      .heatmap-cell[data-intensity='0'] {
        background: #1a2b40;
      }

      .heatmap-cell[data-intensity='1'] {
        background: #1f3a49;
      }

      .heatmap-cell[data-intensity='2'] {
        background: #21514f;
      }

      .heatmap-cell[data-intensity='3'] {
        background: #2f6a4b;
      }

      .heatmap-cell[data-intensity='4'] {
        background: #3a8345;
      }

      .heatmap-cell:hover,
      .heatmap-cell:focus-visible {
        border-color: #9fe9b8;
        outline: none;
      }

      .heatmap-cell-date {
        font-size: 0.72rem;
        color: #c4d8e8;
      }

      .heatmap-cell-count {
        font-weight: 700;
      }

      .trend-list {
        list-style: none;
        margin: 0;
        padding: 0;
        display: grid;
        gap: 0.6rem;
        max-height: 22rem;
        overflow-y: auto;
        scrollbar-width: thin;
        scrollbar-color: #4f7da0 #0f1d31;
      }

      .trend-list::-webkit-scrollbar {
        width: 0.55rem;
      }

      .trend-list::-webkit-scrollbar-thumb {
        background: linear-gradient(180deg, #4f7da0, #3f6483);
        border-radius: 999px;
      }

      .trend-list::-webkit-scrollbar-track {
        background: #0f1d31;
      }

      .trend-card-item {
        margin: 0;
      }

      .trend-card {
        width: 100%;
        border-radius: 0.75rem;
        border: 1px solid #355d79;
        background: #0f1d31;
        color: #ecf6ff;
        padding: 0.7rem 0.75rem;
        text-align: left;
        display: grid;
        gap: 0.35rem;
      }

      .trend-card:hover,
      .trend-card:focus-visible {
        border-color: #85d8ff;
        background: #153150;
      }

      .trend-card-dates,
      .trend-card-metrics {
        display: flex;
        justify-content: space-between;
        gap: 0.35rem;
        flex-wrap: wrap;
      }

      .trend-card-dates {
        color: #9fd7ff;
        font-size: 0.82rem;
      }

      .trend-card-metrics {
        color: #d1e3f2;
        font-size: 0.88rem;
        font-weight: 600;
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

        .xp-band-list {
          grid-template-columns: repeat(2, minmax(0, 1fr));
        }

        .heatmap-grid {
          grid-template-columns: repeat(5, minmax(0, 1fr));
        }

        .heatmap-toolbar {
          align-items: flex-start;
        }

        .month-label {
          min-width: 6.5rem;
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
export class DashboardComponent implements AfterViewInit, OnDestroy {
  private readonly authService = inject(AuthService);
  private readonly progressService = inject(ProgressService);
  private readonly statisticsService = inject(StatisticsService);
  private readonly router = inject(Router);
  private readonly momentumRoutePrefix = '/momentum';
  private readonly momentumSectionId = 'momentum-section';
  private readonly stickyHeaderSelector = '.app-header';
  private readonly scrollRetryDelaysMs = [0, 120, 260];
  private routerEventsSubscription?: { unsubscribe(): void };
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
  selectedWindow = 14;
  heatmapItems: ProgressTrendSummary['items'] = [];
  selectedHeatmapMonthKey = '';
  heatmapMonthOffset = 0;
  heatmapState: DashboardMomentumState = 'loading';
  heatmapError = '';
  private progressRequestVersion = 0;
  private trendRequestVersion = 0;
  private heatmapRequestVersion = 0;
  readonly dailyWindowOptions = [14, 30, 60];
  readonly weeklyWindowOptions = [4, 8, 12];
  readonly minimumHeatmapWindowDays = 7;
  readonly heatmapWindowDays = 90;
  readonly heatmapFallbackWindowDays = 60;
  readonly isSupport = this.authService.hasRole('support');

  constructor() {
    this.selectedHeatmapMonthKey = this.monthKeyForOffset(this.heatmapMonthOffset);
    this.refreshProgress();
    this.refreshStatistics();
    this.refreshMomentum();
    this.refreshHeatmap();
  }

  ngAfterViewInit(): void {
    this.maybeScrollToMomentumSection();
    this.routerEventsSubscription = this.router.events.subscribe((event) => {
      if (event instanceof NavigationEnd) {
        this.maybeScrollToMomentumSection();
      }
    });
  }

  ngOnDestroy(): void {
    this.routerEventsSubscription?.unsubscribe();
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
    const activeRequestVersion = ++this.progressRequestVersion;

    this.progressState = 'loading';
    this.progressError = '';

    forkJoin({
      xpSummary: this.progressService.getXpSummary().pipe(catchError(() => of(null))),
      streakSnapshot: this.progressService.getStreakSnapshot().pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ xpSummary, streakSnapshot }) => {
        if (activeRequestVersion !== this.progressRequestVersion) {
          return;
        }

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
        if (activeRequestVersion !== this.progressRequestVersion) {
          return;
        }

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

  refreshHeatmap(): void {
    const activeRequestVersion = ++this.heatmapRequestVersion;
    const targetMonthKey = this.monthKeyForOffset(this.heatmapMonthOffset);
    const requestedWindowDays = this.windowDaysForHeatmapOffset(this.heatmapMonthOffset);

    this.heatmapState = 'loading';
    this.heatmapError = '';
    this.selectedHeatmapMonthKey = targetMonthKey;

    if (requestedWindowDays > this.heatmapWindowDays) {
      this.heatmapState = 'empty';
      this.heatmapItems = [];
      return;
    }

    this.progressService.getTrendSummary('daily', requestedWindowDays).subscribe({
      next: (trendSummary) => {
        if (activeRequestVersion !== this.heatmapRequestVersion) {
          return;
        }

        this.heatmapItems = trendSummary.items;

        if (this.heatmapVisibleItems().length === 0) {
          this.heatmapState = 'empty';
          return;
        }

        this.heatmapState = 'ready';
      },
      error: (error: ProgressErrorWithCode) => {
        if (activeRequestVersion !== this.heatmapRequestVersion) {
          return;
        }

        if (error?.code === 'validation.request.invalid') {
          const fallbackWindowDays = Math.min(
            this.heatmapWindowDays,
            Math.max(this.minimumHeatmapWindowDays, this.heatmapFallbackWindowDays)
          );
          this.progressService.getTrendSummary('daily', fallbackWindowDays).subscribe({
            next: (trendSummary) => {
              if (activeRequestVersion !== this.heatmapRequestVersion) {
                return;
              }

              this.heatmapItems = trendSummary.items;

              if (this.heatmapVisibleItems().length === 0) {
                this.heatmapState = 'empty';
                return;
              }

              this.heatmapState = 'ready';
            },
            error: () => {
              if (activeRequestVersion !== this.heatmapRequestVersion) {
                return;
              }

              this.heatmapState = 'error';
              this.heatmapError = 'Unable to load monthly heatmap right now. Try again in a moment.';
            }
          });
          return;
        }

        this.heatmapState = 'error';
        this.heatmapError = 'Unable to load monthly heatmap right now. Try again in a moment.';
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
    this.selectedWindow = granularity === 'daily' ? 14 : 12;
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
    const parsedDate = this.parseBucketDate(utcIsoDateTime);
    if (!parsedDate) {
      return utcIsoDateTime.slice(0, 10);
    }

    const year = parsedDate.getUTCFullYear();
    const month = `${parsedDate.getUTCMonth() + 1}`.padStart(2, '0');
    const day = `${parsedDate.getUTCDate()}`.padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  trendCardAriaLabel(utcIsoDateTime: string, completedTaskCount: number, xpGranted: number): string {
    return `Open detail for ${this.formatDateLabel(utcIsoDateTime)}. Completed ${completedTaskCount} tasks and earned ${xpGranted} XP.`;
  }

  heatmapDateLabel(utcIsoDateTime: string): string {
    const parsedDate = this.parseBucketDate(utcIsoDateTime);
    if (!parsedDate) {
      return utcIsoDateTime.slice(8, 10);
    }

    return `${parsedDate.getUTCDate()}`.padStart(2, '0');
  }

  heatmapVisibleItems(): ProgressTrendSummary['items'] {
    if (!this.selectedHeatmapMonthKey) {
      return [];
    }

    return this.heatmapItems.filter((item) => this.monthKeyForBucket(item.bucketStartUtc) === this.selectedHeatmapMonthKey);
  }

  heatmapMonthLabel(): string {
    if (!this.selectedHeatmapMonthKey) {
      return 'No month selected';
    }

    const [year, month] = this.selectedHeatmapMonthKey.split('-').map((value) => Number(value));
    if (!year || !month) {
      return this.selectedHeatmapMonthKey;
    }

    const date = new Date(Date.UTC(year, month - 1, 1));
    return new Intl.DateTimeFormat(undefined, { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(date);
  }

  canShowPreviousMonth(): boolean {
    return this.windowDaysForHeatmapOffset(this.heatmapMonthOffset + 1) <= this.heatmapWindowDays;
  }

  canShowNextMonth(): boolean {
    return this.heatmapMonthOffset > 0;
  }

  showPreviousHeatmapMonth(): void {
    if (!this.canShowPreviousMonth()) {
      return;
    }

    this.heatmapMonthOffset += 1;
    this.refreshHeatmap();
  }

  showNextHeatmapMonth(): void {
    if (!this.canShowNextMonth()) {
      return;
    }

    this.heatmapMonthOffset -= 1;
    this.refreshHeatmap();
  }

  heatmapMaxCompletedCount(): number {
    return this.heatmapVisibleItems().reduce((max, item) => Math.max(max, item.completedTaskCount), 0);
  }

  heatmapIntensityLevel(completedTaskCount: number, maxCount: number): number {
    if (completedTaskCount <= 0 || maxCount <= 0) {
      return 0;
    }

    const ratio = completedTaskCount / maxCount;
    if (ratio <= 0.25) {
      return 1;
    }

    if (ratio <= 0.5) {
      return 2;
    }

    if (ratio <= 0.75) {
      return 3;
    }

    return 4;
  }

  heatmapCellAriaLabel(utcIsoDateTime: string, completedTaskCount: number, xpGranted: number): string {
    const date = this.formatDateLabel(utcIsoDateTime);
    return `${date}: ${completedTaskCount} completed task(s), ${xpGranted} XP earned. Open day detail.`;
  }

  onHeatmapCellKeydown(event: KeyboardEvent, index: number): void {
    const visibleItems = this.heatmapVisibleItems();

    if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.openDayDetail(visibleItems[index].bucketStartUtc);
      return;
    }

    const nextIndex = this.nextHeatmapFocusIndex(event.key, index, visibleItems.length);
    if (nextIndex === index) {
      return;
    }

    event.preventDefault();
    const target = event.currentTarget as HTMLElement | null;
    const grid = target?.closest('.heatmap-grid');
    const nextCell = grid?.querySelector<HTMLElement>(`button[data-index="${nextIndex}"]`);
    nextCell?.focus();
  }

  private monthKeyForBucket(utcIsoDateTime: string): string {
    const parsedDate = this.parseBucketDate(utcIsoDateTime);
    if (!parsedDate) {
      return utcIsoDateTime.slice(0, 7);
    }

    const month = `${parsedDate.getUTCMonth() + 1}`.padStart(2, '0');
    return `${parsedDate.getUTCFullYear()}-${month}`;
  }

  private parseBucketDate(utcIsoDateTime: string): Date | null {
    const parsedDate = new Date(utcIsoDateTime);
    if (Number.isNaN(parsedDate.getTime())) {
      return null;
    }

    return parsedDate;
  }

  private monthKeyForOffset(monthOffset: number): string {
    const targetMonthStart = this.monthStartForOffset(monthOffset);
    const month = `${targetMonthStart.getUTCMonth() + 1}`.padStart(2, '0');
    return `${targetMonthStart.getUTCFullYear()}-${month}`;
  }

  private monthStartForOffset(monthOffset: number): Date {
    const now = new Date();
    const utcYear = now.getUTCFullYear();
    const utcMonth = now.getUTCMonth();
    return new Date(Date.UTC(utcYear, utcMonth - monthOffset, 1));
  }

  private windowDaysForHeatmapOffset(monthOffset: number): number {
    if (monthOffset <= 0) {
      return Math.max(this.minimumHeatmapWindowDays, this.currentUtcDayOfMonth());
    }

    const todayUtc = this.currentUtcDate();
    const targetMonthStartUtc = this.monthStartForOffset(monthOffset);
    const elapsedMilliseconds = todayUtc.getTime() - targetMonthStartUtc.getTime();
    return Math.max(this.minimumHeatmapWindowDays, Math.floor(elapsedMilliseconds / 86_400_000) + 1);
  }

  private currentUtcDate(): Date {
    const now = new Date();
    return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
  }

  private currentUtcDayOfMonth(): number {
    return new Date().getUTCDate();
  }

  nextHeatmapFocusIndex(key: string, index: number, total: number): number {
    const columns = 7;

    if (key === 'ArrowRight') {
      return Math.min(total - 1, index + 1);
    }

    if (key === 'ArrowLeft') {
      return Math.max(0, index - 1);
    }

    if (key === 'ArrowDown') {
      return Math.min(total - 1, index + columns);
    }

    if (key === 'ArrowUp') {
      return Math.max(0, index - columns);
    }

    return index;
  }

  onTrendItemKeydown(event: KeyboardEvent, utcIsoDateTime: string): void {
    if (event.key !== 'Enter' && event.key !== ' ') {
      return;
    }

    event.preventDefault();
    this.openDayDetail(utcIsoDateTime);
  }

  openDayDetail(utcIsoDateTime: string): void {
    const date = this.formatDateLabel(utcIsoDateTime);
    this.router.navigate(['/dashboard/day', date]);
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

  recoveryPrompt(): RecoveryPromptViewModel | null {
    if (!this.streakSnapshot?.isRecoveryPromptVisible) {
      return null;
    }

    const action = this.streakSnapshot.recommendedAction ?? 'complete-task-today';

    const recoveryExplanation = this.streakSnapshot.recoveryExplanation;
    const impactLabel = recoveryExplanation?.message ?? 'Recovery guidance is available for your current streak state.';

    const actionByType: Record<string, { actionLabel: string; actionRoute: string }> = {
      'complete-task-today': {
        actionLabel: 'Complete a task now',
        actionRoute: '/tasks'
      },
      'maintain-tomorrow': {
        actionLabel: 'Review active tasks',
        actionRoute: '/tasks'
      }
    };

    const actionMessage = actionByType[action] ?? actionByType['complete-task-today'];

    return {
      impactLabel,
      impactMessage: `Reason code: ${this.streakSnapshot.recoveryReason ?? recoveryExplanation?.reasonCode ?? 'unknown'}`,
      actionLabel: actionMessage.actionLabel,
      actionRoute: actionMessage.actionRoute,
      announcement: `${impactLabel} ${actionMessage.actionLabel}.`
    };
  }

  xpRemainingToNextLevel(levelProgress: ProgressLevelSnapshot): number {
    return Math.max(0, levelProgress.nextLevelThresholdXp - (this.xpSummary?.totalXp ?? 0));
  }

  xpProgressAriaLabel(levelProgress: ProgressLevelSnapshot): string {
    return `Level ${levelProgress.currentLevel}, ${levelProgress.percentToNextLevel}% to level ${levelProgress.nextLevel}`;
  }

  xpBandSummary(levelProgress: ProgressLevelSnapshot): string {
    if (levelProgress.nextBandLevel === null) {
      return `All configured milestone bands reached (${levelProgress.reachedBandCount} total).`;
    }

    return `${levelProgress.reachedBandCount} milestone bands reached. Next band unlocks at level ${levelProgress.nextBandLevel}.`;
  }

  xpBandState(levelProgress: ProgressLevelSnapshot, bandLevel: number): XpBandState {
    if (levelProgress.currentLevel >= bandLevel) {
      return 'reached';
    }

    if (levelProgress.nextBandLevel === bandLevel) {
      return 'active';
    }

    return 'locked';
  }

  xpBandIcon(levelProgress: ProgressLevelSnapshot, bandLevel: number): string {
    const state = this.xpBandState(levelProgress, bandLevel);
    if (state === 'reached') {
      return 'x';
    }

    if (state === 'active') {
      return '>';
    }

    return 'o';
  }

  xpBandAssistiveLabel(levelProgress: ProgressLevelSnapshot, bandLevel: number): string {
    const state = this.xpBandState(levelProgress, bandLevel);
    if (state === 'reached') {
      return `Milestone level ${bandLevel} reached.`;
    }

    if (state === 'active') {
      return `Milestone level ${bandLevel} is the next target.`;
    }

    return `Milestone level ${bandLevel} is locked.`;
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

  private maybeScrollToMomentumSection(): void {
    const currentUrl = this.router.url.toLowerCase();
    if (!currentUrl.startsWith(this.momentumRoutePrefix)) {
      return;
    }

    for (const delayMs of this.scrollRetryDelaysMs) {
      window.setTimeout(() => {
        const momentumSection = document.getElementById(this.momentumSectionId);
        if (!(momentumSection instanceof HTMLElement)) {
          return;
        }

        const header = document.querySelector(this.stickyHeaderSelector);
        const headerOffset = header instanceof HTMLElement ? header.getBoundingClientRect().height + 8 : 0;
        const targetTop = Math.max(0, window.scrollY + momentumSection.getBoundingClientRect().top - headerOffset);

        window.scrollTo({ top: targetTop, behavior: 'smooth' });
      }, delayMs);
    }
  }
}