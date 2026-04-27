import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../shared/services/auth.service';
import { RegisterComponent } from './register.component';

describe('RegisterComponent', () => {
  let fixture: ComponentFixture<RegisterComponent>;
  let component: RegisterComponent;
  let authService: jasmine.SpyObj<AuthService>;
  let router: Router;

  beforeEach(async () => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['register', 'login']);

    await TestBed.configureTestingModule({
      imports: [RegisterComponent],
      providers: [
        { provide: AuthService, useValue: authService },
        provideRouter([])
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(RegisterComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('shows error when passwords do not match', () => {
    component.form.setValue({
      email: 'user@example.com',
      password: 'StrongPass123!',
      confirmPassword: 'Mismatch123!'
    });

    component.submit();

    expect(component.errorMessage).toContain('Passwords do not match');
    expect(authService.register).not.toHaveBeenCalled();
  });

  it('registers then logs in and navigates', () => {
    authService.register.and.returnValue(of({}));
    authService.login.and.returnValue(of({ accessToken: 'a', refreshToken: 'b', expiresIn: 900 }));

    component.form.setValue({
      email: 'user@example.com',
      password: 'StrongPass123!',
      confirmPassword: 'StrongPass123!'
    });

    component.submit();

    expect(authService.register).toHaveBeenCalledWith({ email: 'user@example.com', password: 'StrongPass123!' });
    expect(authService.login).toHaveBeenCalledWith({ email: 'user@example.com', password: 'StrongPass123!' });
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('shows API error when registration fails', () => {
    authService.register.and.returnValue(throwError(() => new Error('failed')));

    component.form.setValue({
      email: 'user@example.com',
      password: 'StrongPass123!',
      confirmPassword: 'StrongPass123!'
    });

    component.submit();

    expect(component.errorMessage).toContain('Registration failed');
  });
});