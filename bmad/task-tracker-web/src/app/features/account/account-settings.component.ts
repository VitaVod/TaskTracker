import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs/operators';
import { AccountService } from '../../shared/services/account.service';

interface ApiProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

@Component({
  selector: 'app-account-settings',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './account-settings.component.html',
  styleUrl: './account-settings.component.scss'
})
export class AccountSettingsComponent implements OnInit {
  private readonly accountService = inject(AccountService);
  private readonly formBuilder = inject(FormBuilder);

  readonly profileForm = this.formBuilder.nonNullable.group({
    displayName: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(80)]]
  });

  readonly settingsForm = this.formBuilder.nonNullable.group({
    timeZoneId: ['UTC', [Validators.required, Validators.maxLength(64)]],
    locale: ['en-US', [Validators.required, Validators.pattern(/^[a-z]{2}(?:-[A-Z]{2})?$/), Validators.maxLength(16)]],
    leaderboardParticipationMode: ['hidden', [Validators.required]]
  });

  isLoading = true;
  hasLoadedAccount = false;
  isSavingProfile = false;
  isSavingSettings = false;
  profileMessage = '';
  settingsMessage = '';
  profileError = '';
  settingsError = '';
  profileFieldErrors: Record<string, string[]> = {};
  settingsFieldErrors: Record<string, string[]> = {};

  ngOnInit(): void {
    this.accountService.getCurrentUser().subscribe({
      next: (account) => {
        this.profileForm.patchValue({ displayName: account.displayName });
        this.settingsForm.patchValue({
          timeZoneId: account.timeZoneId,
          locale: account.locale,
          leaderboardParticipationMode: account.leaderboardParticipationMode
        });
        this.hasLoadedAccount = true;
        this.isLoading = false;
      },
      error: () => {
        this.hasLoadedAccount = false;
        this.isLoading = false;
        this.profileError = 'Unable to load account data right now.';
      }
    });
  }

  saveProfile(): void {
    this.profileMessage = '';
    this.profileError = '';
    this.profileFieldErrors = {};

    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    if (!this.hasLoadedAccount) {
      this.profileError = 'Refresh account data before saving changes.';
      return;
    }

    this.isSavingProfile = true;
    this.accountService.updateProfile(this.profileForm.getRawValue())
      .pipe(finalize(() => { this.isSavingProfile = false; }))
      .subscribe({
        next: (response) => {
          this.profileMessage = response.message;
        },
        error: (error: unknown) => {
          this.handleError(error, 'profile');
        }
      });
  }

  saveSettings(): void {
    this.settingsMessage = '';
    this.settingsError = '';
    this.settingsFieldErrors = {};

    if (this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      return;
    }

    if (!this.hasLoadedAccount) {
      this.settingsError = 'Refresh account data before saving changes.';
      return;
    }

    this.isSavingSettings = true;
    const settingsPayload = this.settingsForm.getRawValue();
    this.accountService.updateSettings({
      timeZoneId: settingsPayload.timeZoneId,
      locale: settingsPayload.locale,
      leaderboardParticipationMode: settingsPayload.leaderboardParticipationMode as 'public' | 'anonymous' | 'hidden'
    })
      .pipe(finalize(() => { this.isSavingSettings = false; }))
      .subscribe({
        next: (response) => {
          this.settingsMessage = response.message;
        },
        error: (error: unknown) => {
          this.handleError(error, 'settings');
        }
      });
  }

  fieldErrorFor(formKind: 'profile' | 'settings', fieldName: string): string {
    const map = formKind === 'profile' ? this.profileFieldErrors : this.settingsFieldErrors;
    return map[fieldName]?.[0] ?? '';
  }

  private handleError(error: unknown, target: 'profile' | 'settings'): void {
    const details = (error as HttpErrorResponse)?.error as ApiProblemDetails | undefined;
    const fallback = target === 'profile'
      ? 'Profile update failed. Please review your inputs.'
      : 'Settings update failed. Please review your inputs.';

    if (details?.errors) {
      if (target === 'profile') {
        this.profileFieldErrors = details.errors;
      } else {
        this.settingsFieldErrors = details.errors;
      }
    }

    const message = details?.title ?? details?.detail ?? fallback;

    if (target === 'profile') {
      this.profileError = message;
    } else {
      this.settingsError = message;
    }
  }
}
