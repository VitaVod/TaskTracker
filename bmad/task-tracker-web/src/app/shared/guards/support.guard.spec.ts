import { TestBed } from '@angular/core/testing';
import { Router, UrlTree } from '@angular/router';
import { supportGuard } from './support.guard';
import { AuthService } from '../services/auth.service';

describe('supportGuard', () => {
  let authService: jasmine.SpyObj<AuthService>;
  let router: jasmine.SpyObj<Router>;

  beforeEach(() => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['isAuthenticated', 'hasRole']);
    router = jasmine.createSpyObj<Router>('Router', ['createUrlTree']);

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: authService },
        { provide: Router, useValue: router }
      ]
    });
  });

  it('allows support users', () => {
    authService.isAuthenticated.and.returnValue(true);
    authService.hasRole.and.returnValue(true);

    const result = TestBed.runInInjectionContext(() => supportGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });

  it('redirects unauthenticated users to login', () => {
    const loginTree = {} as UrlTree;
    authService.isAuthenticated.and.returnValue(false);
    router.createUrlTree.and.returnValue(loginTree);

    const result = TestBed.runInInjectionContext(() => supportGuard({} as never, {} as never));

    expect(router.createUrlTree).toHaveBeenCalledWith(['/login']);
    expect(result).toBe(loginTree);
  });

  it('redirects authenticated non-support users to dashboard', () => {
    const dashboardTree = {} as UrlTree;
    authService.isAuthenticated.and.returnValue(true);
    authService.hasRole.and.returnValue(false);
    router.createUrlTree.and.returnValue(dashboardTree);

    const result = TestBed.runInInjectionContext(() => supportGuard({} as never, {} as never));

    expect(router.createUrlTree).toHaveBeenCalledWith(['/dashboard']);
    expect(result).toBe(dashboardTree);
  });
});
