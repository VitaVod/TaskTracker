using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Account.Repositories;

public interface IAccountRepository
{
    Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<User?> FindUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    Task<EmailChangeTokenIssuanceResult> IssueEmailChangeTokenAsync(
        Guid userId,
        string normalizedNewEmail,
        TimeSpan lifetime,
        CancellationToken cancellationToken);

    Task<ConfirmEmailChangeResult> ConfirmEmailChangeAsync(
        string confirmationToken,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public record EmailChangeTokenIssuanceResult(
    Guid TokenId,
    Guid UserId,
    string NewEmail,
    string PlainTextToken,
    DateTime ExpiresAtUtc);

public enum ConfirmEmailChangeOutcome
{
    Success,
    InvalidToken,
    ExpiredOrUsedToken,
    TargetEmailUnavailable
}

public record ConfirmEmailChangeResult(
    ConfirmEmailChangeOutcome Outcome,
    string? Error,
    Guid? UserId = null,
    string? PreviousEmail = null,
    string? NewEmail = null);
