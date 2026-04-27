import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { catchError, tap } from 'rxjs/operators';

export interface RegisterPayload {
  email: string;
  password: string;
}

export interface LoginPayload {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface RefreshResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface PasswordRecoveryRequestPayload {
  email: string;
}

export interface PasswordRecoveryConfirmPayload {
  token: string;
  newPassword: string;
}

export interface MessageResponse {
  message: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly httpClient = inject(HttpClient);
  private readonly endpoint = '/api/v1/auth';

  register(payload: RegisterPayload): Observable<unknown> {
    return this.httpClient.post(`${this.endpoint}/register`, payload);
  }

  login(payload: LoginPayload): Observable<LoginResponse> {
    return this.httpClient
      .post<LoginResponse>(`${this.endpoint}/login`, payload)
      .pipe(tap((response) => this.storeTokens(response)));
  }

  requestPasswordRecovery(payload: PasswordRecoveryRequestPayload): Observable<MessageResponse> {
    return this.httpClient.post<MessageResponse>(`${this.endpoint}/password-recovery/request`, payload);
  }

  confirmPasswordRecovery(payload: PasswordRecoveryConfirmPayload): Observable<MessageResponse> {
    return this.httpClient.post<MessageResponse>(`${this.endpoint}/password-recovery/confirm`, payload);
  }

  /**
   * Exchanges the stored refresh token for a new token pair.
   * Stores the new tokens on success and clears them on failure.
   */
  refreshTokens(): Observable<RefreshResponse> {
    const refreshToken = localStorage.getItem('refreshToken');
    if (!refreshToken) {
      this.clearTokens();
      return throwError(() => new Error('No refresh token available'));
    }

    return this.httpClient
      .post<RefreshResponse>(`${this.endpoint}/refresh`, { refreshToken })
      .pipe(
        tap((response) => this.storeTokens(response)),
        catchError((error) => {
          this.clearTokens();
          return throwError(() => error);
        })
      );
  }

  /**
   * Revokes the server-side session and clears local token storage.
   * Returns an observable so callers can await server confirmation.
   */
  logout(): Observable<unknown> {
    const refreshToken = localStorage.getItem('refreshToken') ?? '';
    const accessToken = localStorage.getItem('accessToken') ?? '';

    const request$ = this.httpClient
      .post(`${this.endpoint}/logout`, { refreshToken }, {
        headers: { Authorization: `Bearer ${accessToken}` }
      })
      .pipe(
        tap(() => this.clearTokens()),
        catchError(() => {
          // Clear tokens locally even if the server call fails
          this.clearTokens();
          return throwError(() => new Error('Logout failed'));
        })
      );

    return request$;
  }

  getAccessToken(): string | null {
    return localStorage.getItem('accessToken');
  }

  isAuthenticated(): boolean {
    const token = localStorage.getItem('accessToken');
    if (!token) {
      return false;
    }

    const payload = this.decodeJwtPayload(token);
    if (!payload || typeof payload.exp !== 'number') {
      return false;
    }

    const nowInSeconds = Math.floor(Date.now() / 1000);
    return payload.exp > nowInSeconds;
  }

  private storeTokens(response: { accessToken: string; refreshToken: string }): void {
    localStorage.setItem('accessToken', response.accessToken);
    localStorage.setItem('refreshToken', response.refreshToken);
  }

  clearTokens(): void {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
  }

  private decodeJwtPayload(token: string): { exp?: number } | null {
    const segments = token.split('.');
    if (segments.length !== 3) {
      return null;
    }

    try {
      const payload = this.base64UrlDecode(segments[1]);
      return JSON.parse(payload) as { exp?: number };
    } catch {
      return null;
    }
  }

  private base64UrlDecode(value: string): string {
    const normalized = value.replace(/-/g, '+').replace(/_/g, '/');
    const padding = normalized.length % 4;
    const padded = padding === 0 ? normalized : normalized.padEnd(normalized.length + (4 - padding), '=');

    return atob(padded);
  }
}
