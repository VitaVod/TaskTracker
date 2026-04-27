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
  RouterOutlet
} from '@angular/router';
import { filter } from 'rxjs/operators';
import { LoadingService } from './shared/services/loading.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly router = inject(Router);
  readonly loadingService = inject(LoadingService);
  readonly isLoading = toSignal(this.loadingService.isLoading$, { initialValue: false });

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
}
