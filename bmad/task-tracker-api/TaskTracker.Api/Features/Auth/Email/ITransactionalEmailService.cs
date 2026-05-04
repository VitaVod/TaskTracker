using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Auth.Email;

public interface ITransactionalEmailService
{
    Task<TransactionalEmailSendOutcome> SendPasswordRecoveryAsync(
        PasswordRecoveryEmailMessage message,
        CancellationToken cancellationToken);

    Task<TransactionalEmailSendOutcome> SendTaskReminderAsync(
        TaskReminderEmailMessage message,
        CancellationToken cancellationToken);

    Task<TransactionalEmailSendOutcome> SendAccountSecurityEventAsync(
        AccountSecurityEventEmailMessage message,
        CancellationToken cancellationToken);
}

public record PasswordRecoveryEmailMessage(
    Guid TokenId,
    string ToEmail,
    string RecoveryLink,
    DateTime ExpiresAtUtc);

public record TaskReminderEmailMessage(
    Guid UserId,
    string ToEmail,
    NotificationReminderCadence Cadence,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    IReadOnlyList<TaskReminderTaskSummary> Tasks);

public record TaskReminderTaskSummary(
    Guid TaskId,
    string Title,
    DateTime? DueAtUtc,
    string Priority,
    string Category);

public record AccountSecurityEventEmailMessage(
    Guid UserId,
    string ToEmail,
    AccountSecurityEventType EventType,
    DateTime OccurredAtUtc,
    string CorrelationId,
    IReadOnlyDictionary<string, string> Metadata);

public enum AccountSecurityEventType
{
    PasswordRecoveryRequested,
    PasswordResetCompleted,
    EmailChangeRequested,
    EmailChangeCompleted
}

public enum TransactionalEmailSendResult
{
    Success,
    TransientFailure,
    PermanentFailure
}

public sealed record TransactionalEmailSendOutcome(
    TransactionalEmailSendResult Result,
    string? ProviderMessageId,
    string? ProviderStatus,
    string? ProviderErrorCode)
{
    public static TransactionalEmailSendOutcome Success(string providerMessageId, string providerStatus = "accepted")
    {
        return new TransactionalEmailSendOutcome(TransactionalEmailSendResult.Success, providerMessageId, providerStatus, null);
    }

    public static TransactionalEmailSendOutcome TransientFailure(string? providerErrorCode, string providerStatus = "transient-failure")
    {
        return new TransactionalEmailSendOutcome(TransactionalEmailSendResult.TransientFailure, null, providerStatus, providerErrorCode);
    }

    public static TransactionalEmailSendOutcome PermanentFailure(string? providerErrorCode, string providerStatus = "rejected")
    {
        return new TransactionalEmailSendOutcome(TransactionalEmailSendResult.PermanentFailure, null, providerStatus, providerErrorCode);
    }
}
