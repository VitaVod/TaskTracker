import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../shared/services/auth.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.scss'
})
export class ResetPasswordComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly form = this.formBuilder.nonNullable.group({
    token: ['', [Validators.required]],
    newPassword: [
      '',
      [
        Validators.required,
        Validators.minLength(10),
        Validators.pattern(/(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[^A-Za-z0-9]).+/)
      ]
    ],
    confirmPassword: ['', [Validators.required]]
  });

  isSubmitting = false;
  successMessage = '';
  errorMessage = '';

  constructor() {
    const tokenFromQuery = this.route.snapshot.queryParamMap.get('token');
    if (tokenFromQuery) {
      this.form.controls.token.setValue(tokenFromQuery);
    }
  }

  submit(): void {
    this.successMessage = '';
    this.errorMessage = '';

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { token, newPassword, confirmPassword } = this.form.getRawValue();
    if (newPassword !== confirmPassword) {
      this.errorMessage = 'Passwords do not match.';
      return;
    }

    this.isSubmitting = true;
    this.authService.confirmPasswordRecovery({ token, newPassword }).subscribe({
      next: async (response) => {
        this.isSubmitting = false;
        this.successMessage = response.message;
        await this.router.navigate(['/login']);
      },
      error: (error: HttpErrorResponse) => {
        this.isSubmitting = false;
        this.errorMessage =
          (error.error?.detail as string | undefined) ??
          'Recovery link is invalid. Request a new password recovery email.';
      }
    });
  }
}
