namespace TaskTracker.Api.Features.Auth.Email;

public class LoggingTransactionalEmailService(ILogger<LoggingTransactionalEmailService> logger) : ITransactionalEmailService
{
    public Task<TransactionalEmailSendOutcome> SendPasswordRecoveryAsync(
        PasswordRecoveryEmailMessage message,
        CancellationToken cancellationToken)
    {
        var providerMessageId = $"log-pr-{message.TokenId:N}";
        logger.LogInformation(
            "Transactional email queued: PasswordRecovery token {TokenId} to {Email} (expires {ExpiresAtUtc}). ProviderMessageId={ProviderMessageId}. ProviderStatus=accepted.",
            message.TokenId,
            message.ToEmail,
            message.ExpiresAtUtc,
            providerMessageId);

        return Task.FromResult(TransactionalEmailSendOutcome.Success(providerMessageId));
    }

    public Task<TransactionalEmailSendOutcome> SendTaskReminderAsync(
        TaskReminderEmailMessage message,
        CancellationToken cancellationToken)
    {
        var providerMessageId = $"log-rem-{message.UserId:N}-{message.WindowStartUtc:yyyyMMddHHmmss}";
        logger.LogInformation(
            "Transactional email queued: TaskReminder user {UserId} to {Email} cadence {Cadence} tasks {TaskCount} window {WindowStartUtc}..{WindowEndUtc}. ProviderMessageId={ProviderMessageId}. ProviderStatus=accepted.",
            message.UserId,
            message.ToEmail,
            message.Cadence,
            message.Tasks.Count,
            message.WindowStartUtc,
            message.WindowEndUtc,
            providerMessageId);

        return Task.FromResult(TransactionalEmailSendOutcome.Success(providerMessageId));
    }

    public Task<TransactionalEmailSendOutcome> SendAccountSecurityEventAsync(
        AccountSecurityEventEmailMessage message,
        CancellationToken cancellationToken)
    {
        var providerMessageId = $"log-sec-{message.CorrelationId}";
        logger.LogInformation(
            "Transactional email queued: AccountSecurity event {EventType} user {UserId} to {Email} correlation {CorrelationId} metadata {MetadataCount}. ProviderMessageId={ProviderMessageId}. ProviderStatus=accepted.",
            message.EventType,
            message.UserId,
            message.ToEmail,
            message.CorrelationId,
            message.Metadata.Count,
            providerMessageId);

        return Task.FromResult(TransactionalEmailSendOutcome.Success(providerMessageId));
    }
}
