import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AccountService } from '../../shared/services/account.service';
import { AccountSettingsComponent } from './account-settings.component';

describe('AccountSettingsComponent', () => {
  let fixture: ComponentFixture<AccountSettingsComponent>;
  let component: AccountSettingsComponent;
  let accountService: jasmine.SpyObj<AccountService>;

  beforeEach(async () => {
    accountService = jasmine.createSpyObj<AccountService>('AccountService', [
      'getCurrentUser',
      'updateProfile',
      'updateSettings'
    ]);

    accountService.getCurrentUser.and.returnValue(of({
      userId: 'b304f987-cf8f-4f17-b7ba-599ecb5a4fab',
      email: 'tester@example.com',
      displayName: 'Tester',
      timeZoneId: 'UTC',
      locale: 'en-US',
      leaderboardParticipationMode: 'hidden',
      updatedAtUtc: new Date().toISOString()
    }));

    await TestBed.configureTestingModule({
      imports: [AccountSettingsComponent],
      providers: [
        { provide: AccountService, useValue: accountService },
        provideRouter([])
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(AccountSettingsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('shows inline validation and does not submit invalid profile form', () => {
    component.profileForm.setValue({ displayName: '' });

    component.saveProfile();

    expect(component.profileForm.touched).toBeTrue();
    expect(accountService.updateProfile).not.toHaveBeenCalled();
  });

  it('saves profile successfully and shows success message', () => {
    accountService.updateProfile.and.returnValue(of({ message: 'Profile updated successfully' }));
    component.profileForm.setValue({ displayName: 'Sofia Focus' });

    component.saveProfile();

    expect(accountService.updateProfile).toHaveBeenCalledWith({ displayName: 'Sofia Focus' });
    expect(component.profileMessage).toContain('Profile updated');
  });

  it('renders recoverable field errors while preserving typed settings values', () => {
    accountService.updateSettings.and.returnValue(
      throwError(() => ({
        error: {
          title: 'Validation Error',
          errors: {
            timeZoneId: ['The selected timezone is not valid.']
          }
        }
      }))
    );

    component.settingsForm.setValue({
      timeZoneId: 'Bad/Zone',
      locale: 'en-US',
      leaderboardParticipationMode: 'anonymous'
    });

    component.saveSettings();

    expect(component.settingsFieldErrors['timeZoneId'][0]).toContain('not valid');
    expect(component.settingsForm.getRawValue().timeZoneId).toBe('Bad/Zone');
    expect(component.settingsForm.getRawValue().leaderboardParticipationMode).toBe('anonymous');
  });

  it('blocks saves until fresh account data is loaded', async () => {
    const failingService = jasmine.createSpyObj<AccountService>('AccountService', [
      'getCurrentUser',
      'updateProfile',
      'updateSettings'
    ]);
    failingService.getCurrentUser.and.returnValue(throwError(() => new Error('load failed')));

    await TestBed.resetTestingModule();
    await TestBed.configureTestingModule({
      imports: [AccountSettingsComponent],
      providers: [
        { provide: AccountService, useValue: failingService },
        provideRouter([])
      ]
    }).compileComponents();

    const blockedFixture = TestBed.createComponent(AccountSettingsComponent);
    const blockedComponent = blockedFixture.componentInstance;
    blockedFixture.detectChanges();

    blockedComponent.profileForm.setValue({ displayName: 'Blocked Save' });
    blockedComponent.saveProfile();

    expect(blockedComponent.hasLoadedAccount).toBeFalse();
    expect(failingService.updateProfile).not.toHaveBeenCalled();
  });
});
