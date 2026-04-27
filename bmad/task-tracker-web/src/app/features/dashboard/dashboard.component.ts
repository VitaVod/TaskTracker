import { Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../shared/services/auth.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="dashboard-page">
      <section class="panel">
        <p class="eyebrow">TaskTracker</p>
        <h1>You are signed in</h1>
        <p>Authentication is active. Story 1.2 dashboard route protection is enabled.</p>

        <nav class="actions" aria-label="Dashboard navigation">
          <a routerLink="/tasks" class="settings-link">View task lists</a>
          <a routerLink="/tasks/new" class="settings-link">Create a new task</a>
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
        width: min(100%, 36rem);
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

      .settings-link {
        display: block;
        color: #b8f8f2;
        text-decoration: none;
      }

      .settings-link:hover,
      .settings-link:focus-visible {
        text-decoration: underline;
      }
    `
  ]
})
export class DashboardComponent {
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

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
}