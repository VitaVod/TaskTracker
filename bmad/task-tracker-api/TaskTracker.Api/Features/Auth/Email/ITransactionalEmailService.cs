namespace TaskTracker.Api.Features.Auth.Email;

public interface ITransactionalEmailService
{
    Task<TransactionalEmailSendResult> SendPasswordRecoveryAsync(
        PasswordRecoveryEmailMessage message,
        CancellationToken cancellationToken);
}

public record PasswordRecoveryEmailMessage(
    Guid TokenId,
    string ToEmail,
    string RecoveryLink,
    DateTime ExpiresAtUtc);

public enum TransactionalEmailSendResult
{
    Success,
    TransientFailure,
    PermanentFailure
}
