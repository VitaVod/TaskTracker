import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, throwError } from 'rxjs';
import { catchError, finalize, shareReplay, switchMap } from 'rxjs/operators';
import { AuthService, RefreshResponse } from '../services/auth.service';

/**
 * Shared in-flight refresh request for all interceptor invocations.
 * This prevents duplicate refresh calls and ensures all queued requests either
 * retry together on success or fail together on refresh failure.
 */
let refreshInFlight$: Observable<RefreshResponse> | null = null;

/**
 * HTTP interceptor that:
 * 1. Attaches the current access token as a Bearer header on every outbound request.
 * 2. On 401 responses, attempts exactly one token refresh.
 * 3. Retries the original request with the new access token after a successful refresh.
 * 4. Redirects to /login and clears tokens when refresh fails or the session is gone.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  const requestWithToken = attachToken(req, authService.getAccessToken());

  return next(requestWithToken).pipe(
    catchError((error: unknown) => {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        return handle401(req, next, authService, router);
      }
      return throwError(() => error);
    })
  );
};

function attachToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  if (!token) return req;

  return req.clone({
    setHeaders: { Authorization: `Bearer ${token}` }
  });
}

function handle401(
  originalReq: HttpRequest<unknown>,
  next: HttpHandlerFn,
  authService: AuthService,
  router: Router
): Observable<HttpEvent<unknown>> {
  // Auth endpoints themselves should never be retried — avoid infinite loops.
  if (originalReq.url.includes('/api/v1/auth/')) {
    authService.clearTokens();
    router.navigate(['/login']);
    return throwError(() => new Error('Authentication failed'));
  }

  if (!refreshInFlight$) {
    refreshInFlight$ = authService.refreshTokens().pipe(
      finalize(() => {
        refreshInFlight$ = null;
      }),
      shareReplay({ bufferSize: 1, refCount: false })
    );
  }

  return refreshInFlight$.pipe(
    switchMap((response: RefreshResponse) => {
      return next(attachToken(originalReq, response.accessToken));
    }),
    catchError((error: unknown) => {
      authService.clearTokens();
      router.navigate(['/login']);
      return throwError(() => error);
    })
  );
}
