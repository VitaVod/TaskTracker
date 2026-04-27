using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Auth.Repositories;

public interface IAuthRepository
{
    Task<RegisterRepositoryResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<LoginRepositoryResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task CreateSessionAsync(RefreshSession session, CancellationToken cancellationToken);

    Task<RefreshSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<string?> FindUserRoleAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically revokes <paramref name="oldSessionId"/> and persists <paramref name="newSession"/>
    /// in the same transaction. On replay (old session already rotated), revokes the active
    /// descendant and returns <see cref="RotateSessionOutcome.ReplayDetected"/>.
    /// </summary>
    Task<RotateSessionResult> RotateSessionAsync(
        Guid oldSessionId,
        RefreshSession newSession,
        CancellationToken cancellationToken);

    /// <summary>Revokes an active session. Returns <c>false</c> if not found.</summary>
    Task<bool> RevokeSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken);

    Task<PasswordRecoveryIssuanceResult?> IssuePasswordRecoveryTokenAsync(
        string normalizedEmail,
        TimeSpan lifetime,
        CancellationToken cancellationToken);

    Task RecordPasswordRecoveryDeliveryAttemptAsync(
        Guid tokenId,
        DateTime attemptedAtUtc,
        bool success,
        CancellationToken cancellationToken);

    Task<PasswordResetWithRecoveryResult> ResetPasswordWithRecoveryTokenAsync(
        string recoveryToken,
        string newPassword,
        CancellationToken cancellationToken);
}

public record RegisterRepositoryResult(bool IsSuccess, Guid UserId, string Email, string? Error);

public record LoginRepositoryResult(bool IsSuccess, Guid UserId, string Email, string Role, string? Error);

public enum RotateSessionOutcome
{
    Success,
    NotFound,
    Revoked,
    ReplayDetected
}

public record RotateSessionResult(RotateSessionOutcome Outcome, Guid NewSessionId, string? Error);

public record PasswordRecoveryIssuanceResult(
    Guid TokenId,
    Guid UserId,
    string Email,
    string PlainTextToken,
    DateTime ExpiresAtUtc);

public enum PasswordResetWithRecoveryOutcome
{
    Success,
    InvalidToken,
    ExpiredOrUsedToken,
    InvalidPassword
}

public record PasswordResetWithRecoveryResult(
    PasswordResetWithRecoveryOutcome Outcome,
    string? Error);
