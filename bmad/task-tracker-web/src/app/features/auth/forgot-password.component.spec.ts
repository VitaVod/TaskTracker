import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../shared/services/auth.service';
import { ForgotPasswordComponent } from './forgot-password.component';

describe('ForgotPasswordComponent', () => {
  let fixture: ComponentFixture<ForgotPasswordComponent>;
  let component: ForgotPasswordComponent;
  let authService: jasmine.SpyObj<AuthService>;

  beforeEach(async () => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['requestPasswordRecovery']);

    await TestBed.configureTestingModule({
      imports: [ForgotPasswordComponent],
      providers: [{ provide: AuthService, useValue: authService }, provideRouter([])]
    }).compileComponents();

    fixture = TestBed.createComponent(ForgotPasswordComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('marks form touched and skips submit when invalid', () => {
    component.submit();

    expect(component.form.touched).toBeTrue();
    expect(authService.requestPasswordRecovery).not.toHaveBeenCalled();
  });

  it('submits and shows deterministic success message', () => {
    authService.requestPasswordRecovery.and.returnValue(
      of({ message: 'If the account exists, a recovery email has been sent.' })
    );

    component.form.setValue({ email: 'user@example.com' });
    component.submit();

    expect(authService.requestPasswordRecovery).toHaveBeenCalledWith({ email: 'user@example.com' });
    expect(component.successMessage).toContain('If the account exists');
  });

  it('shows API error when request fails', () => {
    authService.requestPasswordRecovery.and.returnValue(throwError(() => new Error('failure')));

    component.form.setValue({ email: 'user@example.com' });
    component.submit();

    expect(component.errorMessage).toContain('Unable to process recovery');
  });
});
