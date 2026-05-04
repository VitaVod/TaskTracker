import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  NotificationPreferencesResponse,
  NotificationPreferencesUpdateRequest,
  NotificationPreferencesUpdateResponse
} from '../models/notification-preferences.models';

@Injectable({ providedIn: 'root' })
export class NotificationPreferencesService {
  private readonly httpClient = inject(HttpClient);
  private readonly endpoint = '/api/v1/notifications/preferences';

  getPreferences(): Observable<NotificationPreferencesResponse> {
    return this.httpClient.get<NotificationPreferencesResponse>(this.endpoint);
  }

  updatePreferences(payload: NotificationPreferencesUpdateRequest): Observable<NotificationPreferencesUpdateResponse> {
    return this.httpClient.patch<NotificationPreferencesUpdateResponse>(this.endpoint, payload);
  }
}
