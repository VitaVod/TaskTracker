import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { authInterceptor } from '../interceptors/auth.interceptor';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);

    localStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  // ── login / token storage ──────────────────────────────────────────────────

  it('stores tokens in localStorage after a successful login', () => {
    service.login({ email: 'user@example.com', password: 'Pass123!' }).subscribe();

    httpMock.expectOne('/api/v1/auth/login').flush({
      accessToken: 'access-token',
      refreshToken: 'refresh-token',
      expiresIn: 900
    });

    expect(localStorage.getItem('accessToken')).toBe('access-token');
    expect(localStorage.getItem('refreshToken')).toBe('refresh-token');
  });

  // ── logout ─────────────────────────────────────────────────────────────────

  it('calls the logout endpoint with the stored refresh token', () => {
    localStorage.setItem('accessToken', 'stored-access');
    localStorage.setItem('refreshToken', 'stored-refresh');

    service.logout().subscribe();

    const req = httpMock.expectOne('/api/v1/auth/logout');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'stored-refresh' });
    req.flush({ message: 'Session revoked successfully' });

    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
  });

  it('clears tokens even when the logout server call fails', () => {
    localStorage.setItem('accessToken', 'stored-access');
    localStorage.setItem('refreshToken', 'stored-refresh');

    service.logout().subscribe({ error: () => {} });

    httpMock.expectOne('/api/v1/auth/logout').error(new ProgressEvent('network-error'));

    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
  });

  // ── refreshTokens ──────────────────────────────────────────────────────────

  it('exchanges the refresh token and stores new tokens', () => {
    localStorage.setItem('refreshToken', 'old-refresh');

    service.refreshTokens().subscribe();

    const req = httpMock.expectOne('/api/v1/auth/refresh');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ refreshToken: 'old-refresh' });

    req.flush({ accessToken: 'new-access', refreshToken: 'new-refresh', expiresIn: 900 });

    expect(localStorage.getItem('accessToken')).toBe('new-access');
    expect(localStorage.getItem('refreshToken')).toBe('new-refresh');
  });

  it('clears tokens when refresh fails', () => {
    localStorage.setItem('accessToken', 'old-access');
    localStorage.setItem('refreshToken', 'old-refresh');

    service.refreshTokens().subscribe({ error: () => {} });

    httpMock.expectOne('/api/v1/auth/refresh').flush(
      { title: 'Session Invalid' },
      { status: 401, statusText: 'Unauthorized' }
    );

    expect(localStorage.getItem('accessToken')).toBeNull();
    expect(localStorage.getItem('refreshToken')).toBeNull();
  });

  it('returns error immediately when no refresh token is stored', () => {
    let errorEmitted = false;

    service.refreshTokens().subscribe({ error: () => { errorEmitted = true; } });

    httpMock.expectNone('/api/v1/auth/refresh');
    expect(errorEmitted).toBeTrue();
  });

  // ── isAuthenticated ────────────────────────────────────────────────────────

  it('returns false when no access token is stored', () => {
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('returns false for a malformed token', () => {
    localStorage.setItem('accessToken', 'not.a.jwt');
    expect(service.isAuthenticated()).toBeFalse();
  });

  it('extracts admin role from ClaimTypes.Role payload', () => {
    localStorage.setItem('accessToken', buildJwt({
      exp: Math.floor(Date.now() / 1000) + 3600,
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'Admin'
    }));

    expect(service.getCurrentRole()).toBe('admin');
    expect(service.hasRole('admin')).toBeTrue();
  });

  it('returns null role when no supported role claim exists', () => {
    localStorage.setItem('accessToken', buildJwt({
      exp: Math.floor(Date.now() / 1000) + 3600,
      scope: 'tasks.read'
    }));

    expect(service.getCurrentRole()).toBeNull();
    expect(service.hasRole('admin')).toBeFalse();
  });

  // ── password recovery ─────────────────────────────────────────────────────

  it('requests password recovery for an email', () => {
    service.requestPasswordRecovery({ email: 'recover@example.com' }).subscribe();

    const req = httpMock.expectOne('/api/v1/auth/password-recovery/request');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'recover@example.com' });
    req.flush({ message: 'If the account exists, a recovery email has been sent.' });
  });

  it('confirms password recovery with token and new password', () => {
    service.confirmPasswordRecovery({ token: 'token', newPassword: 'NewStrongPass456!' }).subscribe();

    const req = httpMock.expectOne('/api/v1/auth/password-recovery/confirm');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ token: 'token', newPassword: 'NewStrongPass456!' });
    req.flush({ message: 'Password updated successfully' });
  });
});

describe('authInterceptor', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;
  let router: Router;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: 'login', component: class {} }])
      ]
    });

    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    localStorage.clear();
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('attaches the Bearer token to outgoing requests', () => {
    localStorage.setItem('accessToken', 'valid-access');

    service.register({ email: 'x@x.com', password: 'pass' }).subscribe();

    const req = httpMock.expectOne('/api/v1/auth/register');
    expect(req.request.headers.get('Authorization')).toBe('Bearer valid-access');
    req.flush({});
  });

  it('redirects to /login and clears tokens when refresh itself fails with 401', () => {
    localStorage.setItem('accessToken', 'expired-access');
    localStorage.setItem('refreshToken', 'expired-refresh');

    // A non-auth request triggers the interceptor's 401 handler
    service['httpClient'].get('/api/v1/tasks').subscribe({ error: () => {} });

    // Original request returns 401
    httpMock.expectOne('/api/v1/tasks').flush(
      { title: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' }
    );

    // Interceptor attempts refresh
    const refreshReq = httpMock.expectOne('/api/v1/auth/refresh');
    refreshReq.flush(
      { title: 'Session Invalid' },
      { status: 401, statusText: 'Unauthorized' }
    );

    expect(router.navigate).toHaveBeenCalledWith(['/login']);
    expect(localStorage.getItem('accessToken')).toBeNull();
  });

  it('fails concurrent 401 requests together when refresh fails', () => {
    localStorage.setItem('accessToken', 'expired-access');
    localStorage.setItem('refreshToken', 'expired-refresh');

    let firstErrored = false;
    let secondErrored = false;

    service['httpClient'].get('/api/v1/tasks/one').subscribe({ error: () => { firstErrored = true; } });
    service['httpClient'].get('/api/v1/tasks/two').subscribe({ error: () => { secondErrored = true; } });

    httpMock.expectOne('/api/v1/tasks/one').flush(
      { title: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' }
    );

    httpMock.expectOne('/api/v1/tasks/two').flush(
      { title: 'Unauthorized' },
      { status: 401, statusText: 'Unauthorized' }
    );

    httpMock.expectOne('/api/v1/auth/refresh').flush(
      { title: 'Session Invalid' },
      { status: 401, statusText: 'Unauthorized' }
    );

    expect(firstErrored).toBeTrue();
    expect(secondErrored).toBeTrue();
  });

  it('does not clear tokens or redirect on 403 responses', () => {
    localStorage.setItem('accessToken', 'valid-access');
    localStorage.setItem('refreshToken', 'valid-refresh');

    service['httpClient'].get('/api/v1/ops/admin/health').subscribe({ error: () => {} });

    httpMock.expectOne('/api/v1/ops/admin/health').flush(
      { title: 'Forbidden', code: 'authz.access.denied' },
      { status: 403, statusText: 'Forbidden' }
    );

    httpMock.expectNone('/api/v1/auth/refresh');
    expect(router.navigate).not.toHaveBeenCalled();
    expect(localStorage.getItem('accessToken')).toBe('valid-access');
    expect(localStorage.getItem('refreshToken')).toBe('valid-refresh');
  });
});

function buildJwt(payload: Record<string, unknown>): string {
  const header = base64UrlEncode({ alg: 'HS256', typ: 'JWT' });
  const body = base64UrlEncode(payload);
  return `${header}.${body}.signature`;
}

function base64UrlEncode(value: Record<string, unknown>): string {
  const raw = btoa(JSON.stringify(value));
  return raw.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '');
}
