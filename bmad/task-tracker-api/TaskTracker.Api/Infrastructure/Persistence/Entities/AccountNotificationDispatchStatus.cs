namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public enum AccountNotificationDispatchStatus
{
    Queued = 0,
    Processing = 1,
    Succeeded = 2,
    FailedTransient = 3,
    FailedPermanent = 4
}
