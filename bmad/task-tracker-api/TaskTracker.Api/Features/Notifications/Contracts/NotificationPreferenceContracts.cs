namespace TaskTracker.Api.Features.Notifications.Contracts;

public record NotificationPreferencesResponse(
    bool ReminderEmailEnabled,
    string ReminderCadence,
    bool AccountEmailEnabled,
    DateTime UpdatedAtUtc);

public record NotificationPreferencesUpdateResponse(string Message);
