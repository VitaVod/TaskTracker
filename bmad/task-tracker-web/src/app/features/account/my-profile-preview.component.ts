import { CommonModule } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { AccountService } from '../../shared/services/account.service';
import { ProgressService } from '../../shared/services/progress.service';

type MyProfileState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-my-profile-preview',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <main class="preview-page">
      <section class="preview-shell">
        <header>
          <p class="eyebrow">TaskTracker / My profile</p>
          <h1>How Others See You</h1>
          <p class="subtitle">This preview mirrors your current leaderboard visibility settings.</p>
          <a routerLink="/profile" class="back-link">Open settings</a>
        </header>

        @if (state === 'loading') {
          <section class="state-card" aria-live="polite" aria-busy="true">
            <p>Loading profile preview...</p>
          </section>
        } @else if (state === 'error') {
          <section class="state-card" role="alert" aria-live="assertive">
            <p>{{ errorMessage }}</p>
            <button type="button" (click)="refresh()">Retry</button>
          </section>
        } @else {
          <article class="preview-card" aria-live="polite">
            <p class="metric-label">Public identity</p>
            <p class="identity">{{ publicIdentityLabel }}</p>
            <p class="metric-meta">Visibility mode: {{ visibilityLabel }}</p>

            <div class="metrics-grid">
              <section>
                <p class="metric-label">Current streak</p>
                <p class="metric-value">{{ currentStreakDays }} day(s)</p>
              </section>
              <section>
                <p class="metric-label">Total XP</p>
                <p class="metric-value">{{ totalXp }}</p>
              </section>
            </div>

            <p class="hint">Tip: update display name and visibility mode in Settings to control what appears publicly.</p>
          </article>
        }
      </section>
    </main>
  `,
  styles: [
    `
      .preview-page {
        min-height: 100vh;
        padding: 1.5rem;
        display: grid;
        place-items: center;
      }

      .preview-shell {
        width: min(100%, 42rem);
        border-radius: 1rem;
        padding: 1.5rem;
        border: 1px solid rgba(124, 182, 255, 0.28);
        background: linear-gradient(155deg, rgba(11, 20, 39, 0.92), rgba(18, 36, 58, 0.9));
      }

      .eyebrow {
        margin: 0;
        text-transform: uppercase;
        letter-spacing: 0.08em;
        font-size: 0.75rem;
        color: #9cc5ff;
      }

      .subtitle {
        margin-top: 0.25rem;
        color: #d0def2;
      }

      .back-link {
        color: #9ff4cb;
      }

      .state-card,
      .preview-card {
        margin-top: 1rem;
        border-radius: 0.85rem;
        border: 1px solid #3b5679;
        background: #12233a;
        padding: 1rem;
      }

      .metric-label {
        margin: 0;
        font-size: 0.74rem;
        text-transform: uppercase;
        letter-spacing: 0.06em;
        color: #9fc7ff;
      }

      .identity {
        margin: 0.35rem 0 0;
        font-size: 1.35rem;
        font-weight: 700;
        color: #f3f8ff;
      }

      .metric-meta {
        margin: 0.45rem 0 0;
        color: #d0def2;
      }

      .metrics-grid {
        margin-top: 0.85rem;
        display: grid;
        gap: 0.7rem;
        grid-template-columns: repeat(2, minmax(0, 1fr));
      }

      .metric-value {
        margin: 0.3rem 0 0;
        font-size: 1.2rem;
        font-weight: 700;
      }

      .hint {
        margin-top: 0.9rem;
        color: #c8ddf7;
      }

      button {
        margin-top: 0.7rem;
        border: 1px solid #6cb6ff;
        border-radius: 0.55rem;
        background: #19456e;
        color: #f3f8ff;
        font-weight: 600;
        padding: 0.45rem 0.7rem;
      }

      @media (max-width: 768px) {
        .metrics-grid {
          grid-template-columns: 1fr;
        }
      }
    `
  ]
})
export class MyProfilePreviewComponent {
  private readonly accountService = inject(AccountService);
  private readonly progressService = inject(ProgressService);

  state: MyProfileState = 'loading';
  errorMessage = '';
  publicIdentityLabel = 'Anonymous participant';
  visibilityLabel = 'Anonymous';
  currentStreakDays = 0;
  totalXp = 0;

  constructor() {
    this.refresh();
  }

  refresh(): void {
    this.state = 'loading';
    this.errorMessage = '';

    forkJoin({
      account: this.accountService.getCurrentUser().pipe(catchError(() => of(null))),
      xp: this.progressService.getXpSummary().pipe(catchError(() => of(null))),
      streak: this.progressService.getStreakSnapshot().pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ account, xp, streak }) => {
        if (!account) {
          this.state = 'error';
          this.errorMessage = 'Unable to load your profile preview right now.';
          return;
        }

        this.publicIdentityLabel = account.leaderboardParticipationMode === 'public'
          ? account.displayName
          : account.leaderboardParticipationMode === 'anonymous'
            ? 'Anonymous participant'
            : 'Hidden from leaderboards';

        this.visibilityLabel = account.leaderboardParticipationMode === 'public'
          ? 'Public display name'
          : account.leaderboardParticipationMode === 'anonymous'
            ? 'Anonymous alias'
            : 'Hidden';

        this.currentStreakDays = streak?.currentStreakDays ?? 0;
        this.totalXp = xp?.totalXp ?? 0;
        this.state = 'ready';
      },
      error: () => {
        this.state = 'error';
        this.errorMessage = 'Unable to load your profile preview right now.';
      }
    });
  }
}
