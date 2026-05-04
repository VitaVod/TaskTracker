using System.Text.Json;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Notifications.Validation;

public interface INotificationPreferencesValidator
{
    NotificationPreferencesPatchValidationResult ValidatePatch(JsonElement payload);
}

public sealed record NotificationPreferencesPatchValidationResult(
    bool IsValid,
    bool HasReminderEmailEnabled,
    bool ReminderEmailEnabled,
    bool HasReminderCadence,
    NotificationReminderCadence ReminderCadence,
    bool HasAccountEmailEnabled,
    bool AccountEmailEnabled,
    Dictionary<string, string[]> Errors);

public class NotificationPreferencesValidator : INotificationPreferencesValidator
{
    public NotificationPreferencesPatchValidationResult ValidatePatch(JsonElement payload)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (payload.ValueKind != JsonValueKind.Object)
        {
            errors["$"] = ["Request body must be a JSON object."];
            return new NotificationPreferencesPatchValidationResult(
                false,
                false,
                true,
                false,
                NotificationReminderCadence.Daily,
                false,
                true,
                errors);
        }

        var hasReminderEmailEnabled = false;
        var hasReminderCadence = false;
        var hasAccountEmailEnabled = false;
        var reminderEmailEnabled = true;
        var reminderCadence = NotificationReminderCadence.Daily;
        var accountEmailEnabled = true;

        foreach (var property in payload.EnumerateObject())
        {
            switch (property.Name)
            {
                case "reminderEmailEnabled":
                    hasReminderEmailEnabled = true;
                    if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                    {
                        errors["reminderEmailEnabled"] = ["Reminder email enabled must be a boolean value."];
                        break;
                    }

                    reminderEmailEnabled = property.Value.GetBoolean();
                    break;

                case "reminderCadence":
                    hasReminderCadence = true;
                    if (property.Value.ValueKind != JsonValueKind.String)
                    {
                        errors["reminderCadence"] = ["Reminder cadence must be a string value."];
                        break;
                    }

                    var cadenceValue = property.Value.GetString()?.Trim() ?? string.Empty;
                    if (string.Equals(cadenceValue, "daily", StringComparison.OrdinalIgnoreCase))
                    {
                        reminderCadence = NotificationReminderCadence.Daily;
                    }
                    else if (string.Equals(cadenceValue, "weekly", StringComparison.OrdinalIgnoreCase))
                    {
                        reminderCadence = NotificationReminderCadence.Weekly;
                    }
                    else
                    {
                        errors["reminderCadence"] = ["Reminder cadence must be one of: daily, weekly."];
                    }

                    break;

                case "accountEmailEnabled":
                    hasAccountEmailEnabled = true;
                    if (property.Value.ValueKind != JsonValueKind.True && property.Value.ValueKind != JsonValueKind.False)
                    {
                        errors["accountEmailEnabled"] = ["Account email enabled must be a boolean value."];
                        break;
                    }

                    accountEmailEnabled = property.Value.GetBoolean();
                    break;

                default:
                    errors[property.Name] = ["This field cannot be updated."];
                    break;
            }
        }

        if (!hasReminderEmailEnabled && !hasReminderCadence && !hasAccountEmailEnabled)
        {
            errors["$"] = ["At least one notification preference field must be provided."];
        }

        return new NotificationPreferencesPatchValidationResult(
            errors.Count == 0,
            hasReminderEmailEnabled,
            reminderEmailEnabled,
            hasReminderCadence,
            reminderCadence,
            hasAccountEmailEnabled,
            accountEmailEnabled,
            errors);
    }
}
