import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../shared/services/auth.service';

@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss'
})
export class ForgotPasswordComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);

  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]]
  });

  isSubmitting = false;
  successMessage = '';
  errorMessage = '';

  submit(): void {
    this.successMessage = '';
    this.errorMessage = '';

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.authService.requestPasswordRecovery(this.form.getRawValue()).subscribe({
      next: (response) => {
        this.isSubmitting = false;
        this.successMessage = response.message;
      },
      error: () => {
        this.isSubmitting = false;
        this.errorMessage = 'Unable to process recovery right now. Please try again in a moment.';
      }
    });
  }
}
