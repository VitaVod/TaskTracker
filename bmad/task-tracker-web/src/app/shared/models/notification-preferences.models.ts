export type ReminderCadence = 'daily' | 'weekly';

export interface NotificationPreferencesResponse {
  reminderEmailEnabled: boolean;
  reminderCadence: ReminderCadence;
  accountEmailEnabled: boolean;
  updatedAtUtc: string;
}

export interface NotificationPreferencesUpdateRequest {
  reminderEmailEnabled?: boolean;
  reminderCadence?: ReminderCadence;
  accountEmailEnabled?: boolean;
}

export interface NotificationPreferencesUpdateResponse {
  message: string;
}
