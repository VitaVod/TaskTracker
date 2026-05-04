namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public enum AccountNotificationEventType
{
    PasswordRecoveryRequested = 0,
    PasswordResetCompleted = 1,
    EmailChangeRequested = 2,
    EmailChangeCompleted = 3
}
