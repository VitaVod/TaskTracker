using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Features.Auth.Email;
using TaskTracker.Api.Features.Auth.Repositories;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Notifications.AccountEvents;

public class AccountEventNotificationService(
    TaskTrackerDbContext dbContext,
    ITransactionalEmailService transactionalEmailService,
    IAuthRepository authRepository,
    ILogger<AccountEventNotificationService> logger) : IAccountEventNotificationService
{
    private const int MaxDeliveryAttempts = 3;

    public async Task NotifyPasswordRecoveryRequestedAsync(
        Guid userId,
        string toEmail,
        Guid tokenId,
        string recoveryLink,
        DateTime expiresAtUtc,
        string traceId,
        CancellationToken cancellationToken)
    {
        await NotifyAsync(
            userId,
            toEmail,
            AccountNotificationEventType.PasswordRecoveryRequested,
            $"password-recovery-requested:{tokenId:N}",
            traceId,
            correlationId: tokenId.ToString("N"),
            sendAsync: token => transactionalEmailService.SendPasswordRecoveryAsync(
                new PasswordRecoveryEmailMessage(tokenId, toEmail, recoveryLink, expiresAtUtc),
                token),
            cancellationToken);
    }

    public async Task NotifyPasswordResetCompletedAsync(
        Guid userId,
        string toEmail,
        string traceId,
        CancellationToken cancellationToken)
    {
        var occurrenceId = Guid.NewGuid().ToString("N");
        await NotifyAsync(
            userId,
            toEmail,
            AccountNotificationEventType.PasswordResetCompleted,
            $"password-reset-completed:{userId:N}:{traceId}",
            traceId,
            correlationId: occurrenceId,
            sendAsync: token => transactionalEmailService.SendAccountSecurityEventAsync(
                new AccountSecurityEventEmailMessage(
                    userId,
                    toEmail,
                    AccountSecurityEventType.PasswordResetCompleted,
                    DateTime.UtcNow,
                    occurrenceId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["event"] = "password-reset-completed"
                    }),
                token),
            cancellationToken);
    }

    public async Task NotifyEmailChangeRequestedAsync(
        Guid userId,
        string toEmail,
        Guid tokenId,
        string confirmationLink,
        DateTime expiresAtUtc,
        string traceId,
        CancellationToken cancellationToken)
    {
        await NotifyAsync(
            userId,
            toEmail,
            AccountNotificationEventType.EmailChangeRequested,
            $"email-change-requested:{tokenId:N}",
            traceId,
            correlationId: tokenId.ToString("N"),
            sendAsync: token => transactionalEmailService.SendAccountSecurityEventAsync(
                new AccountSecurityEventEmailMessage(
                    userId,
                    toEmail,
                    AccountSecurityEventType.EmailChangeRequested,
                    DateTime.UtcNow,
                    tokenId.ToString("N"),
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["event"] = "email-change-requested",
                        ["confirmationLink"] = confirmationLink,
                        ["expiresAtUtc"] = expiresAtUtc.ToString("O")
                    }),
                token),
            cancellationToken);
    }

    public async Task NotifyEmailChangeCompletedAsync(
        Guid userId,
        string previousEmail,
        string newEmail,
        string traceId,
        CancellationToken cancellationToken)
    {
        var occurrenceId = Guid.NewGuid().ToString("N");
        await NotifyAsync(
            userId,
            previousEmail,
            AccountNotificationEventType.EmailChangeCompleted,
            $"email-change-completed:{userId:N}:{traceId}",
            traceId,
            correlationId: occurrenceId,
            sendAsync: token => transactionalEmailService.SendAccountSecurityEventAsync(
                new AccountSecurityEventEmailMessage(
                    userId,
                    previousEmail,
                    AccountSecurityEventType.EmailChangeCompleted,
                    DateTime.UtcNow,
                    occurrenceId,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["event"] = "email-change-completed",
                        ["newEmail"] = newEmail
                    }),
                token),
            cancellationToken);
    }

    private async Task NotifyAsync(
        Guid userId,
        string toEmail,
        AccountNotificationEventType eventType,
        string eventKey,
        string traceId,
        string correlationId,
        Func<CancellationToken, Task<TransactionalEmailSendOutcome>> sendAsync,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(existingUser => existingUser.Id == userId, cancellationToken);

        if (user is null)
        {
            logger.LogWarning(
                "Account notification skipped because user was not found. EventType={EventType}. UserId={UserId}. TraceId={TraceId}",
                eventType,
                userId,
                traceId);
            return;
        }

        if (!user.AccountEmailEnabled)
        {
            logger.LogInformation(
                "Account notification skipped by preference. EventType={EventType}. UserId={UserId}. TraceId={TraceId}",
                eventType,
                userId,
                traceId);
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var dispatch = new AccountNotificationDispatch
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            EventType = eventType,
            EventKey = eventKey,
            ToEmail = toEmail,
            Status = AccountNotificationDispatchStatus.Queued,
            AttemptCount = 0,
            CreatedAtUtc = nowUtc,
            LastUpdatedAtUtc = nowUtc,
            TraceId = traceId,
            CorrelationId = correlationId
        };

        dbContext.AccountNotificationDispatches.Add(dispatch);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            dbContext.Entry(dispatch).State = EntityState.Detached;
            logger.LogInformation(
                "Account notification deduplicated by event key. EventType={EventType}. EventKey={EventKey}. UserId={UserId}. TraceId={TraceId}",
                eventType,
                eventKey,
                userId,
                traceId);
            return;
        }

        dispatch.Status = AccountNotificationDispatchStatus.Processing;
        dispatch.LastUpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        TransactionalEmailSendOutcome? lastOutcome = null;
        for (var attempt = 1; attempt <= MaxDeliveryAttempts; attempt++)
        {
            logger.LogInformation(
                "Account notification delivery attempt {Attempt}/{MaxAttempts}. EventType={EventType}. UserId={UserId}. CorrelationId={CorrelationId}. TraceId={TraceId}",
                attempt,
                MaxDeliveryAttempts,
                eventType,
                userId,
                correlationId,
                traceId);

            dispatch.AttemptCount = attempt;
            dispatch.LastAttemptAtUtc = DateTime.UtcNow;
            dispatch.LastUpdatedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            TransactionalEmailSendOutcome sendOutcome;
            try
            {
                sendOutcome = await sendAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Account notification provider threw an exception. EventType={EventType}. UserId={UserId}. Attempt={Attempt}. CorrelationId={CorrelationId}. TraceId={TraceId}",
                    eventType,
                    userId,
                    attempt,
                    correlationId,
                    traceId);

                sendOutcome = TransactionalEmailSendOutcome.TransientFailure(
                    providerErrorCode: ex.GetType().Name,
                    providerStatus: "exception");
            }

            lastOutcome = sendOutcome;
            var sendResult = sendOutcome.Result;
            var success = sendResult == TransactionalEmailSendResult.Success;

            logger.LogInformation(
                "Account notification provider response. EventType={EventType}. UserId={UserId}. Attempt={Attempt}. SendResult={SendResult}. ProviderStatus={ProviderStatus}. ProviderMessageId={ProviderMessageId}. ProviderErrorCode={ProviderErrorCode}. CorrelationId={CorrelationId}. TraceId={TraceId}",
                eventType,
                userId,
                attempt,
                sendResult,
                sendOutcome.ProviderStatus ?? "unknown",
                sendOutcome.ProviderMessageId ?? "none",
                sendOutcome.ProviderErrorCode ?? "none",
                correlationId,
                traceId);

            if (eventType == AccountNotificationEventType.PasswordRecoveryRequested
                && Guid.TryParse(correlationId, out var tokenId))
            {
                await authRepository.RecordPasswordRecoveryDeliveryAttemptAsync(
                    tokenId,
                    DateTime.UtcNow,
                    success,
                    cancellationToken);
            }

            if (success)
            {
                dispatch.Status = AccountNotificationDispatchStatus.Succeeded;
                dispatch.SentAtUtc = DateTime.UtcNow;
                dispatch.LastFailureCategory = null;
                dispatch.LastUpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Account notification delivery succeeded. EventType={EventType}. UserId={UserId}. Attempt={Attempt}. ProviderMessageId={ProviderMessageId}. CorrelationId={CorrelationId}. TraceId={TraceId}",
                    eventType,
                    userId,
                    attempt,
                    sendOutcome.ProviderMessageId ?? "none",
                    correlationId,
                    traceId);
                return;
            }

            if (sendResult == TransactionalEmailSendResult.PermanentFailure)
            {
                dispatch.Status = AccountNotificationDispatchStatus.FailedPermanent;
                dispatch.LastFailureCategory = "permanent";
                dispatch.LastUpdatedAtUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogWarning(
                    "Account notification delivery failed permanently. EventType={EventType}. UserId={UserId}. ProviderStatus={ProviderStatus}. ProviderErrorCode={ProviderErrorCode}. CorrelationId={CorrelationId}. TraceId={TraceId}",
                    eventType,
                    userId,
                    sendOutcome.ProviderStatus ?? "unknown",
                    sendOutcome.ProviderErrorCode ?? "none",
                    correlationId,
                    traceId);
                return;
            }
        }

        dispatch.Status = AccountNotificationDispatchStatus.FailedTransient;
        dispatch.LastFailureCategory = "transient";
        dispatch.LastUpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogError(
            "Account notification delivery exhausted transient retries. EventType={EventType}. UserId={UserId}. LastProviderStatus={ProviderStatus}. LastProviderErrorCode={ProviderErrorCode}. CorrelationId={CorrelationId}. TraceId={TraceId}",
            eventType,
            userId,
            lastOutcome?.ProviderStatus ?? "unknown",
            lastOutcome?.ProviderErrorCode ?? "none",
            correlationId,
            traceId);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        var sqlException = exception.InnerException as SqlException;
        return sqlException is not null && (sqlException.Number == 2627 || sqlException.Number == 2601);
    }
}
