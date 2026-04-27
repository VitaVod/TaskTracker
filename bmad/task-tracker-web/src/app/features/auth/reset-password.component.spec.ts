import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../shared/services/auth.service';
import { ResetPasswordComponent } from './reset-password.component';

describe('ResetPasswordComponent', () => {
  let fixture: ComponentFixture<ResetPasswordComponent>;
  let component: ResetPasswordComponent;
  let authService: jasmine.SpyObj<AuthService>;
  let router: Router;

  beforeEach(async () => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['confirmPasswordRecovery']);

    await TestBed.configureTestingModule({
      imports: [ResetPasswordComponent],
      providers: [
        { provide: AuthService, useValue: authService },
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { snapshot: { queryParamMap: convertToParamMap({ token: 'token-from-link' }) } }
        }
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(ResetPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('prefills token from query string', () => {
    expect(component.form.controls.token.value).toBe('token-from-link');
  });

  it('shows error when passwords do not match', () => {
    component.form.setValue({
      token: 'token-from-link',
      newPassword: 'StrongPass123!',
      confirmPassword: 'Mismatch123!'
    });

    component.submit();

    expect(component.errorMessage).toContain('Passwords do not match');
    expect(authService.confirmPasswordRecovery).not.toHaveBeenCalled();
  });

  it('submits reset and navigates to login on success', () => {
    authService.confirmPasswordRecovery.and.returnValue(of({ message: 'Password updated successfully' }));

    component.form.setValue({
      token: 'token-from-link',
      newPassword: 'StrongPass123!',
      confirmPassword: 'StrongPass123!'
    });

    component.submit();

    expect(authService.confirmPasswordRecovery).toHaveBeenCalledWith({
      token: 'token-from-link',
      newPassword: 'StrongPass123!'
    });
    expect(router.navigate).toHaveBeenCalledWith(['/login']);
  });

  it('renders API detail guidance for invalid recovery link', () => {
    authService.confirmPasswordRecovery.and.returnValue(
      throwError(() => ({ error: { detail: 'This recovery link is expired or already used.' } }))
    );

    component.form.setValue({
      token: 'token-from-link',
      newPassword: 'StrongPass123!',
      confirmPassword: 'StrongPass123!'
    });

    component.submit();

    expect(component.errorMessage).toContain('expired or already used');
  });
});
