import { Component, inject } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationStart,
  RouteConfigLoadEnd,
  RouteConfigLoadStart,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet
} from '@angular/router';
import { filter, finalize } from 'rxjs/operators';
import { AuthService } from './shared/services/auth.service';
import { LoadingService } from './shared/services/loading.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly router = inject(Router);
  private readonly authService = inject(AuthService);
  readonly primaryTabs = [
    { label: 'Dashboard', icon: 'D', route: '/dashboard' },
    { label: 'Tasks', icon: 'T', route: '/tasks' },
    { label: 'Leaderboard', icon: 'L', route: '/leaderboard' },
    { label: 'My Profile', icon: 'P', route: '/my-profile' },
    { label: 'Settings', icon: 'S', route: '/profile' }
  ] as const;
  readonly loadingService = inject(LoadingService);
  readonly isLoading = toSignal(this.loadingService.isLoading$, { initialValue: false });
  isLoggingOut = false;

  constructor() {
    this.router.events
      .pipe(
        filter((event) =>
          event instanceof NavigationStart
          || event instanceof RouteConfigLoadStart
          || event instanceof NavigationEnd
          || event instanceof RouteConfigLoadEnd
          || event instanceof NavigationCancel
          || event instanceof NavigationError
        ),
        takeUntilDestroyed()
      )
      .subscribe((event) => {
        if (event instanceof NavigationStart || event instanceof RouteConfigLoadStart) {
          this.loadingService.start();
          return;
        }

        this.loadingService.stop();
      });
  }

  showPrimaryTabs(): boolean {
    if (this.authService.isAuthenticated()) {
      return true;
    }

    const currentUrl = this.router.url.toLowerCase();
    return currentUrl !== '/'
      && !currentUrl.startsWith('/landing')
      && !currentUrl.startsWith('/login')
      && !currentUrl.startsWith('/register')
      && !currentUrl.startsWith('/forgot-password')
      && !currentUrl.startsWith('/reset-password');
  }

  onLogout(): void {
    if (this.isLoggingOut) {
      return;
    }

    this.isLoggingOut = true;
    this.authService
      .logout()
      .pipe(finalize(() => {
        this.isLoggingOut = false;
      }))
      .subscribe({
        next: () => {
          void this.router.navigate(['/landing']);
        },
        error: () => {
          void this.router.navigate(['/landing']);
        }
      });
  }
}
