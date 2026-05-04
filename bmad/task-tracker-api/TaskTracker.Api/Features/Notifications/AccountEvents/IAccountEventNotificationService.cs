namespace TaskTracker.Api.Features.Notifications.AccountEvents;

public interface IAccountEventNotificationService
{
    Task NotifyPasswordRecoveryRequestedAsync(
        Guid userId,
        string toEmail,
        Guid tokenId,
        string recoveryLink,
        DateTime expiresAtUtc,
        string traceId,
        CancellationToken cancellationToken);

    Task NotifyPasswordResetCompletedAsync(
        Guid userId,
        string toEmail,
        string traceId,
        CancellationToken cancellationToken);

    Task NotifyEmailChangeRequestedAsync(
        Guid userId,
        string toEmail,
        Guid tokenId,
        string confirmationLink,
        DateTime expiresAtUtc,
        string traceId,
        CancellationToken cancellationToken);

    Task NotifyEmailChangeCompletedAsync(
        Guid userId,
        string previousEmail,
        string newEmail,
        string traceId,
        CancellationToken cancellationToken);
}
