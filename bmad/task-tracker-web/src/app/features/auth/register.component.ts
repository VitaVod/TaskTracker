import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../../shared/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent {
  private readonly formBuilder = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly form = this.formBuilder.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(10),
        Validators.pattern(/(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[^A-Za-z0-9]).+/)
      ]
    ],
    confirmPassword: ['', [Validators.required]]
  });

  errorMessage = '';
  isSubmitting = false;

  submit(): void {
    this.errorMessage = '';

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password, confirmPassword } = this.form.getRawValue();
    if (password !== confirmPassword) {
      this.errorMessage = 'Passwords do not match.';
      return;
    }

    this.isSubmitting = true;
    this.authService.register({ email, password }).subscribe({
      next: () => {
        this.authService.login({ email, password }).subscribe({
          next: async () => {
            this.isSubmitting = false;
            await this.router.navigate(['/dashboard']);
          },
          error: () => {
            this.isSubmitting = false;
            this.errorMessage = 'Account created, but sign in failed. Please log in.';
          }
        });
      },
      error: () => {
        this.isSubmitting = false;
        this.errorMessage = 'Registration failed. Check the form details and try again.';
      }
    });
  }
}