namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public enum NotificationReminderDispatchStatus
{
    Processing = 0,
    Succeeded = 1,
    FailedTransient = 2,
    FailedPermanent = 3
}