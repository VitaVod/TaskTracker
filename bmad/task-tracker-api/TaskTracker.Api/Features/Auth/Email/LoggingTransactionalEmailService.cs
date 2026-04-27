namespace TaskTracker.Api.Features.Auth.Email;

public class LoggingTransactionalEmailService(ILogger<LoggingTransactionalEmailService> logger) : ITransactionalEmailService
{
    public Task<TransactionalEmailSendResult> SendPasswordRecoveryAsync(
        PasswordRecoveryEmailMessage message,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Transactional email queued: PasswordRecovery token {TokenId} to {Email} (expires {ExpiresAtUtc}).",
            message.TokenId,
            message.ToEmail,
            message.ExpiresAtUtc);

        return Task.FromResult(TransactionalEmailSendResult.Success);
    }
}
