import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../shared/services/auth.service';
import { LoginComponent } from './login.component';

describe('LoginComponent', () => {
  let fixture: ComponentFixture<LoginComponent>;
  let component: LoginComponent;
  let authService: jasmine.SpyObj<AuthService>;
  let router: Router;

  beforeEach(async () => {
    authService = jasmine.createSpyObj<AuthService>('AuthService', ['login']);

    await TestBed.configureTestingModule({
      imports: [LoginComponent],
      providers: [
        { provide: AuthService, useValue: authService },
        provideRouter([])
      ]
    }).compileComponents();

    router = TestBed.inject(Router);
    spyOn(router, 'navigate').and.resolveTo(true);

    fixture = TestBed.createComponent(LoginComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('marks form touched and skips submit when invalid', () => {
    component.submit();

    expect(component.form.touched).toBeTrue();
    expect(authService.login).not.toHaveBeenCalled();
  });

  it('logs in and navigates when credentials are valid', () => {
    authService.login.and.returnValue(of({ accessToken: 'a', refreshToken: 'b', expiresIn: 900 }));

    component.form.setValue({
      email: 'user@example.com',
      password: 'StrongPass123!'
    });

    component.submit();

    expect(authService.login).toHaveBeenCalledWith({
      email: 'user@example.com',
      password: 'StrongPass123!'
    });
    expect(router.navigate).toHaveBeenCalledWith(['/dashboard']);
  });

  it('shows API error when login fails', () => {
    authService.login.and.returnValue(throwError(() => new Error('failure')));

    component.form.setValue({
      email: 'user@example.com',
      password: 'StrongPass123!'
    });

    component.submit();

    expect(component.errorMessage).toContain('Sign in failed');
  });
});