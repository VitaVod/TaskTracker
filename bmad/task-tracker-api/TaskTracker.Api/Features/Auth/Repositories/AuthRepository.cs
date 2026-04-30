using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Features.Auth.Security;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Auth.Repositories;

public class AuthRepository(
    TaskTrackerDbContext dbContext,
    IPasswordHasher passwordHasher,
    ILogger<AuthRepository> logger) : IAuthRepository
{
    public async Task<RegisterRepositoryResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (!IsEmailValid(normalizedEmail))
        {
            return new RegisterRepositoryResult(false, Guid.Empty, string.Empty, "Email is not in a valid format.");
        }

        if (!IsPasswordValid(request.Password, out var passwordProblem))
        {
            return new RegisterRepositoryResult(false, Guid.Empty, string.Empty, passwordProblem);
        }

        var existingUser = await dbContext.Users.AnyAsync(user => user.Email == normalizedEmail, cancellationToken);
        if (existingUser)
        {
            return new RegisterRepositoryResult(false, Guid.Empty, string.Empty, "Email already registered.");
        }

        var (hash, salt) = passwordHasher.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            DisplayName = string.Empty,
            TimeZoneId = "UTC",
            Locale = "en-US",
            Role = AppRoles.User,
            LeaderboardParticipationMode = LeaderboardParticipationMode.Hidden,
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedAtUtc = DateTime.UtcNow
        };

        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateEmailViolation(exception))
        {
            return new RegisterRepositoryResult(false, Guid.Empty, string.Empty, "Email already registered.");
        }

        return new RegisterRepositoryResult(true, user.Id, user.Email, null);
    }

    public async Task<LoginRepositoryResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(existingUser => existingUser.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Failed login attempt for unknown email {Email}", normalizedEmail);
            return new LoginRepositoryResult(false, Guid.Empty, string.Empty, string.Empty, "Invalid email or password");
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            logger.LogWarning("Failed login attempt for user {UserId}", user.Id);
            return new LoginRepositoryResult(false, Guid.Empty, string.Empty, string.Empty, "Invalid email or password");
        }

        return new LoginRepositoryResult(true, user.Id, user.Email, user.Role, null);
    }

    public async Task CreateSessionAsync(RefreshSession session, CancellationToken cancellationToken)
    {
        dbContext.RefreshSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RefreshSession?> FindSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        return await dbContext.RefreshSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
    }

    public async Task<string?> FindUserRoleAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .Where(user => user.Id == userId)
            .Select(user => user.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RotateSessionResult> RotateSessionAsync(
        Guid oldSessionId,
        RefreshSession newSession,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var oldSession = await dbContext.RefreshSessions
                .FirstOrDefaultAsync(s => s.Id == oldSessionId, cancellationToken);

            if (oldSession is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new RotateSessionResult(RotateSessionOutcome.NotFound, Guid.Empty, "Refresh session not found.");
            }

            // Replay detection: the presented token was already rotated
            if (oldSession.RevokedAtUtc is not null && oldSession.RevokedReason == "rotated")
            {
                logger.LogWarning(
                    "Replay attack detected: session {OldSessionId} was already rotated to {ReplacedBy}. Revoking active chain.",
                    oldSessionId,
                    oldSession.ReplacedBySessionId);

                await RevokeActiveDescendantAsync(oldSession, "replay-detected", cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new RotateSessionResult(RotateSessionOutcome.ReplayDetected, Guid.Empty, "Refresh token replay detected.");
            }

            if (oldSession.RevokedAtUtc is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new RotateSessionResult(RotateSessionOutcome.Revoked, Guid.Empty, "Session has been revoked.");
            }

            var now = DateTime.UtcNow;
            oldSession.RevokedAtUtc = now;
            oldSession.RevokedReason = "rotated";
            oldSession.ReplacedBySessionId = newSession.Id;

            dbContext.RefreshSessions.Add(newSession);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(cancellationToken);

                // Another request rotated/revoked this session first. Treat as replay-safe failure.
                var latestOldSession = await dbContext.RefreshSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == oldSessionId, cancellationToken);

                if (latestOldSession is null)
                {
                    return new RotateSessionResult(RotateSessionOutcome.NotFound, Guid.Empty, "Refresh session not found.");
                }

                if (latestOldSession.RevokedReason == "rotated")
                {
                    var trackedLatest = await dbContext.RefreshSessions
                        .FirstOrDefaultAsync(s => s.Id == oldSessionId, cancellationToken);

                    if (trackedLatest is not null)
                    {
                        await RevokeActiveDescendantAsync(trackedLatest, "replay-detected", cancellationToken);
                        await dbContext.SaveChangesAsync(cancellationToken);
                    }

                    return new RotateSessionResult(RotateSessionOutcome.ReplayDetected, Guid.Empty, "Refresh token replay detected.");
                }

                return new RotateSessionResult(RotateSessionOutcome.Revoked, Guid.Empty, "Session has been revoked.");
            }

            logger.LogInformation(
                "Session rotated: {OldSessionId} -> {NewSessionId} for user {UserId}",
                oldSessionId,
                newSession.Id,
                newSession.UserId);

            return new RotateSessionResult(RotateSessionOutcome.Success, newSession.Id, null);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> RevokeSessionAsync(Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        var session = await dbContext.RefreshSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken);

        if (session is null)
        {
            return false;
        }

        if (session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = DateTime.UtcNow;
            session.RevokedReason = reason;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Session {SessionId} revoked for user {UserId} (reason: {Reason})",
                sessionId,
                session.UserId,
                reason);
        }

        return true;
    }

    public async Task<PasswordRecoveryIssuanceResult?> IssuePasswordRecoveryTokenAsync(
        string normalizedEmail,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(existingUser => existingUser.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var activeTokens = await dbContext.PasswordRecoveryTokens
            .Where(token => token.UserId == user.Id && token.UsedAtUtc == null && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = now;
        }

        var tokenId = Guid.NewGuid();
        var plainSecret = CreateRandomSecret();
        var tokenHash = ComputeTokenHash(plainSecret);

        var entity = new PasswordRecoveryToken
        {
            TokenId = tokenId,
            UserId = user.Id,
            TokenHash = tokenHash,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime),
            DeliveryAttemptCount = 0,
            CreatedAtUtc = now
        };

        dbContext.PasswordRecoveryTokens.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var wireToken = $"{tokenId:N}.{plainSecret}";
        return new PasswordRecoveryIssuanceResult(tokenId, user.Id, user.Email, wireToken, entity.ExpiresAtUtc);
    }

    public async Task RecordPasswordRecoveryDeliveryAttemptAsync(
        Guid tokenId,
        DateTime attemptedAtUtc,
        bool success,
        CancellationToken cancellationToken)
    {
        var token = await dbContext.PasswordRecoveryTokens
            .FirstOrDefaultAsync(existingToken => existingToken.TokenId == tokenId, cancellationToken);

        if (token is null)
        {
            return;
        }

        token.DeliveryAttemptCount += 1;
        token.LastDeliveryAttemptAtUtc = attemptedAtUtc;

        if (!success)
        {
            logger.LogWarning(
                "Password recovery delivery attempt failed for token {TokenId}. Attempt={AttemptCount}",
                tokenId,
                token.DeliveryAttemptCount);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PasswordResetWithRecoveryResult> ResetPasswordWithRecoveryTokenAsync(
        string recoveryToken,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (!IsPasswordValid(newPassword, out var passwordError))
        {
            return new PasswordResetWithRecoveryResult(PasswordResetWithRecoveryOutcome.InvalidPassword, passwordError);
        }

        if (!TryParseRecoveryToken(recoveryToken, out var tokenId, out var tokenSecret))
        {
            return new PasswordResetWithRecoveryResult(PasswordResetWithRecoveryOutcome.InvalidToken, "Invalid recovery token format.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var token = await dbContext.PasswordRecoveryTokens
                .AsNoTracking()
                .FirstOrDefaultAsync(existingToken => existingToken.TokenId == tokenId, cancellationToken);

            if (token is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PasswordResetWithRecoveryResult(PasswordResetWithRecoveryOutcome.InvalidToken, "Recovery token could not be found.");
            }

            var now = DateTime.UtcNow;

            if (token.UsedAtUtc is not null || token.RevokedAtUtc is not null || token.ExpiresAtUtc <= now)
            {
                if (token.RevokedAtUtc is null && token.ExpiresAtUtc <= now)
                {
                    token.RevokedAtUtc = now;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return new PasswordResetWithRecoveryResult(PasswordResetWithRecoveryOutcome.ExpiredOrUsedToken, "Recovery token is expired or already used.");
            }

            var expectedHash = token.TokenHash;
            var presentedHash = ComputeTokenHash(tokenSecret);
            if (!FixedTimeEquals(expectedHash, presentedHash))
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PasswordResetWithRecoveryResult(PasswordResetWithRecoveryOutcome.InvalidToken, "Recovery token hash mismatch.");
            }

            var consumed = await TryConsumePasswordRecoveryTokenAsync(tokenId, now, cancellationToken);
            if (!consumed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PasswordResetWithRecoveryResult(PasswordResetWithRecoveryOutcome.ExpiredOrUsedToken, "Recovery token is expired or already used.");
            }

            var user = await dbContext.Users
                .FirstOrDefaultAsync(existingUser => existingUser.Id == token.UserId, cancellationToken);

            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return new PasswordResetWithRecoveryResult(PasswordResetWithRecoveryOutcome.InvalidToken, "Recovery token user was not found.");
            }

            var (hash, salt) = passwordHasher.HashPassword(newPassword);
            user.PasswordHash = hash;
            user.PasswordSalt = salt;
            user.ModifiedAtUtc = now;

            var sessions = await dbContext.RefreshSessions
                .Where(session => session.UserId == user.Id && session.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var session in sessions)
            {
                session.RevokedAtUtc = now;
                session.RevokedReason = "password-reset";
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new PasswordResetWithRecoveryResult(PasswordResetWithRecoveryOutcome.Success, null);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task RevokeActiveDescendantAsync(
        RefreshSession start,
        string reason,
        CancellationToken cancellationToken)
    {
        var current = start;
        const int maxDepth = 50;
        for (var depth = 0; depth < maxDepth && current.ReplacedBySessionId is not null; depth++)
        {
            var next = await dbContext.RefreshSessions
                .FirstOrDefaultAsync(s => s.Id == current.ReplacedBySessionId, cancellationToken);

            if (next is null) break;

            if (next.RevokedAtUtc is null)
            {
                next.RevokedAtUtc = DateTime.UtcNow;
                next.RevokedReason = reason;
                return;
            }

            current = next;
        }
    }

    private static string CreateRandomSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    private static string ComputeTokenHash(string secret)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hash);
    }

    private static bool FixedTimeEquals(string expectedHash, string presentedHash)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
        var presentedBytes = Encoding.UTF8.GetBytes(presentedHash);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, presentedBytes);
    }

    private static bool TryParseRecoveryToken(string recoveryToken, out Guid tokenId, out string tokenSecret)
    {
        tokenId = Guid.Empty;
        tokenSecret = string.Empty;

        if (string.IsNullOrWhiteSpace(recoveryToken))
        {
            return false;
        }

        var parts = recoveryToken.Split('.', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out tokenId))
        {
            return false;
        }

        tokenSecret = parts[1];
        return !string.IsNullOrWhiteSpace(tokenSecret);
    }

    private async Task<bool> TryConsumePasswordRecoveryTokenAsync(
        Guid tokenId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        try
        {
            // On relational providers this is atomic and prevents parallel reuse.
            var consumedCount = await dbContext.PasswordRecoveryTokens
                .Where(existingToken => existingToken.TokenId == tokenId
                    && existingToken.UsedAtUtc == null
                    && existingToken.RevokedAtUtc == null
                    && existingToken.ExpiresAtUtc > now)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(existingToken => existingToken.UsedAtUtc, now),
                    cancellationToken);

            return consumedCount == 1;
        }
        catch (InvalidOperationException)
        {
            // In-memory provider used by tests does not support ExecuteUpdate.
            var trackedToken = await dbContext.PasswordRecoveryTokens
                .FirstOrDefaultAsync(existingToken => existingToken.TokenId == tokenId
                    && existingToken.UsedAtUtc == null
                    && existingToken.RevokedAtUtc == null
                    && existingToken.ExpiresAtUtc > now,
                    cancellationToken);

            if (trackedToken is null)
            {
                return false;
            }

            trackedToken.UsedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
    }

    private static bool IsEmailValid(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsPasswordValid(string password, out string problem)
    {
        if (password.Length < 10)
        {
            problem = "Password must be at least 10 characters long.";
            return false;
        }

        if (!password.Any(char.IsUpper))
        {
            problem = "Password must contain at least one uppercase letter.";
            return false;
        }

        if (!password.Any(char.IsLower))
        {
            problem = "Password must contain at least one lowercase letter.";
            return false;
        }

        if (!password.Any(char.IsDigit))
        {
            problem = "Password must contain at least one digit.";
            return false;
        }

        if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            problem = "Password must contain at least one non-alphanumeric character.";
            return false;
        }

        problem = string.Empty;
        return true;
    }

    private static bool IsDuplicateEmailViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sqlException && (sqlException.Number == 2601 || sqlException.Number == 2627))
            {
                return true;
            }
        }

        return false;
    }
}
