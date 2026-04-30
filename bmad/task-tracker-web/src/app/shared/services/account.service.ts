import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface AccountMeResponse {
  userId: string;
  email: string;
  displayName: string;
  timeZoneId: string;
  locale: string;
  leaderboardParticipationMode: 'public' | 'anonymous' | 'hidden';
  updatedAtUtc: string;
}

export interface AccountUpdateResponse {
  message: string;
}

export interface UpdateProfilePayload {
  displayName: string;
}

export interface UpdateSettingsPayload {
  timeZoneId?: string;
  locale?: string;
  leaderboardParticipationMode?: 'public' | 'anonymous' | 'hidden';
}

@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly httpClient = inject(HttpClient);
  private readonly endpoint = '/api/v1/account';

  getCurrentUser(): Observable<AccountMeResponse> {
    return this.httpClient.get<AccountMeResponse>(`${this.endpoint}/me`);
  }

  updateProfile(payload: UpdateProfilePayload): Observable<AccountUpdateResponse> {
    return this.httpClient.patch<AccountUpdateResponse>(`${this.endpoint}/profile`, payload);
  }

  updateSettings(payload: UpdateSettingsPayload): Observable<AccountUpdateResponse> {
    return this.httpClient.patch<AccountUpdateResponse>(`${this.endpoint}/settings`, payload);
  }
}
