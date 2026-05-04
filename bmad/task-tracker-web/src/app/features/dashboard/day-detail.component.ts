import { Component, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { TaskResponse } from '../../shared/models/task.models';
import { ProgressService } from '../../shared/services/progress.service';
import { TaskService } from '../../shared/services/task.service';

type DayDetailState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-day-detail',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="day-detail-page">
      <section class="panel">
        <p class="eyebrow">TaskTracker</p>
        <h1>Day detail: {{ selectedDate }}</h1>
        <p>Review completions, XP, and streak context for this day.</p>

        @if (state === 'loading') {
          <section class="state-card" aria-live="polite" aria-busy="true">
            <p class="state-title">Loading day detail...</p>
          </section>
        } @else if (state === 'error') {
          <section class="state-card" role="alert" aria-live="assertive">
            <p class="state-title">{{ errorMessage }}</p>
          </section>
        } @else {
          <section class="detail-grid" aria-label="Day detail summary">
            <article class="metric-card">
              <p class="metric-label">Completed tasks</p>
              <p class="metric-value">{{ completedTasks.length }}</p>
            </article>
            <article class="metric-card">
              <p class="metric-label">XP granted</p>
              <p class="metric-value">{{ xpGranted }}</p>
            </article>
            <article class="metric-card">
              <p class="metric-label">Streak impact</p>
              <p class="metric-value">{{ streakImpactLabel }}</p>
            </article>
            <article class="metric-card">
              <p class="metric-label">Momentum score</p>
              <p class="metric-value">{{ momentumScore }}</p>
              <p class="metric-meta">Formula: task completions x 15 + XP + streak bonus.</p>
            </article>
          </section>

          <section class="task-list-panel" aria-labelledby="completed-heading">
            <h2 id="completed-heading">Completed tasks for this day</h2>
            @if (completedTasks.length === 0) {
              <p class="metric-meta">No completed tasks were recorded for this date.</p>
            } @else {
              <ul>
                @for (task of completedTasks; track task.id) {
                  <li>
                    <span class="task-title">{{ task.title }}</span>
                    <span class="metric-meta">Updated {{ task.updatedAtUtc.slice(0, 10) }}</span>
                  </li>
                }
              </ul>
            }
          </section>
        }

        <nav class="actions" aria-label="Day detail navigation">
          <a routerLink="/dashboard" class="settings-link">Back to dashboard</a>
          <a routerLink="/tasks" class="settings-link">Open task lists</a>
        </nav>
      </section>
    </main>
  `,
  styles: [
    `
      .day-detail-page {
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
        margin: 0.45rem 0;
      }

      .state-card,
      .metric-card,
      .task-list-panel {
        border-radius: 0.75rem;
        border: 1px solid #355d79;
        background: #0f1d31;
        padding: 0.85rem;
      }

      .state-title,
      .metric-meta {
        margin: 0.35rem 0 0;
        color: #c2d8e7;
      }

      .detail-grid {
        margin-top: 0.9rem;
        display: grid;
        grid-template-columns: repeat(2, minmax(0, 1fr));
        gap: 0.7rem;
      }

      .metric-label {
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        font-size: 0.73rem;
        color: #9fd7ff;
      }

      .metric-value {
        margin: 0.3rem 0 0;
        font-size: 1.25rem;
        font-weight: 700;
      }

      .task-list-panel {
        margin-top: 0.75rem;
      }

      .task-list-panel h2 {
        margin: 0;
        font-size: 1.05rem;
      }

      .task-list-panel ul {
        margin: 0.7rem 0 0;
        padding: 0;
        list-style: none;
        display: grid;
        gap: 0.5rem;
      }

      .task-list-panel li {
        border-radius: 0.6rem;
        border: 1px solid rgba(101, 148, 179, 0.45);
        padding: 0.55rem 0.6rem;
        display: grid;
        gap: 0.2rem;
      }

      .task-title {
        font-weight: 600;
      }

      .actions {
        margin-top: 1rem;
        display: grid;
        gap: 0.6rem;
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

        .detail-grid {
          grid-template-columns: 1fr;
        }
      }
    `
  ]
})
export class DayDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly progressService = inject(ProgressService);
  private readonly taskService = inject(TaskService);

  selectedDate = '';
  completedTasks: TaskResponse[] = [];
  xpGranted = 0;
  streakImpactLabel = 'No current streak bonus';
  momentumScore = 0;
  state: DayDetailState = 'loading';
  errorMessage = '';

  constructor() {
    this.route.paramMap.subscribe((params) => {
      const date = params.get('date') ?? '';
      if (!this.isIsoDate(date)) {
        this.state = 'error';
        this.errorMessage = 'Invalid day selected. Return to dashboard and try again.';
        return;
      }

      this.selectedDate = date;
      this.loadDayDetail(date);
    });
  }

  private loadDayDetail(date: string): void {
    this.state = 'loading';
    this.errorMessage = '';

    forkJoin({
      trendSummary: this.progressService.getTrendSummary('daily', 31),
      streakSnapshot: this.progressService.getStreakSnapshot(),
      completedTasks: this.taskService.getTasks('completed')
    }).subscribe({
      next: ({ trendSummary, streakSnapshot, completedTasks }) => {
        const matchingPoint = trendSummary.items.find((item) => item.bucketStartUtc.slice(0, 10) === date);
        this.xpGranted = matchingPoint?.xpGranted ?? 0;

        this.completedTasks = completedTasks.items.filter(
          (task) => task.isCompleted && task.updatedAtUtc.slice(0, 10) === date
        );

        const streakBonus = streakSnapshot.currentStreakDays > 0 ? Math.min(50, streakSnapshot.currentStreakDays) : 0;
        this.streakImpactLabel = streakBonus > 0 ? `+${streakBonus} streak bonus` : 'No current streak bonus';
        this.momentumScore = this.completedTasks.length * 15 + this.xpGranted + streakBonus;
        this.state = 'ready';
      },
      error: () => {
        this.state = 'error';
        this.errorMessage = 'Unable to load day detail right now. Try again in a moment.';
      }
    });
  }

  private isIsoDate(value: string): boolean {
    return /^\d{4}-\d{2}-\d{2}$/.test(value);
  }
}
