using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;
using System.Text;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Account.Repositories;

public class AccountRepository(TaskTrackerDbContext dbContext) : IAccountRepository
{
    public async Task<User?> FindUserByIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users.FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<User?> FindUserByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        return await dbContext.Users.FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);
    }

    public async Task<EmailChangeTokenIssuanceResult> IssueEmailChangeTokenAsync(
        Guid userId,
        string normalizedNewEmail,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activeTokens = await dbContext.EmailChangeTokens
            .Where(token => token.UserId == userId && token.UsedAtUtc == null && token.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = now;
        }

        var tokenId = Guid.NewGuid();
        var secret = CreateRandomSecret();
        var tokenHash = ComputeTokenHash(secret);

        var entity = new EmailChangeToken
        {
            TokenId = tokenId,
            UserId = userId,
            NewEmail = normalizedNewEmail,
            TokenHash = tokenHash,
            RequestedAtUtc = now,
            ExpiresAtUtc = now.Add(lifetime),
            CreatedAtUtc = now
        };

        dbContext.EmailChangeTokens.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var wireToken = $"{tokenId:N}.{secret}";
        return new EmailChangeTokenIssuanceResult(tokenId, userId, normalizedNewEmail, wireToken, entity.ExpiresAtUtc);
    }

    public async Task<ConfirmEmailChangeResult> ConfirmEmailChangeAsync(
        string confirmationToken,
        CancellationToken cancellationToken)
    {
        if (!TryParseConfirmationToken(confirmationToken, out var tokenId, out var tokenSecret))
        {
            return new ConfirmEmailChangeResult(ConfirmEmailChangeOutcome.InvalidToken, "Invalid confirmation token format.");
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var token = await dbContext.EmailChangeTokens
                    .AsNoTracking()
                    .FirstOrDefaultAsync(existingToken => existingToken.TokenId == tokenId, cancellationToken);

                if (token is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ConfirmEmailChangeResult(ConfirmEmailChangeOutcome.InvalidToken, "Confirmation token could not be found.");
                }

                var now = DateTime.UtcNow;

                if (token.UsedAtUtc is not null || token.RevokedAtUtc is not null || token.ExpiresAtUtc <= now)
                {
                    if (token.RevokedAtUtc is null && token.ExpiresAtUtc <= now)
                    {
                        try
                        {
                            await dbContext.EmailChangeTokens
                                .Where(existingToken => existingToken.TokenId == token.TokenId && existingToken.RevokedAtUtc == null)
                                .ExecuteUpdateAsync(update => update.SetProperty(existingToken => existingToken.RevokedAtUtc, now), cancellationToken);
                        }
                        catch (InvalidOperationException)
                        {
                            var trackedExpiredToken = await dbContext.EmailChangeTokens
                                .FirstOrDefaultAsync(existingToken => existingToken.TokenId == token.TokenId && existingToken.RevokedAtUtc == null, cancellationToken);

                            if (trackedExpiredToken is not null)
                            {
                                trackedExpiredToken.RevokedAtUtc = now;
                                await dbContext.SaveChangesAsync(cancellationToken);
                            }
                        }
                    }

                    await transaction.CommitAsync(cancellationToken);
                    return new ConfirmEmailChangeResult(ConfirmEmailChangeOutcome.ExpiredOrUsedToken, "Confirmation token is expired or already used.");
                }

                var expectedHash = token.TokenHash;
                var presentedHash = ComputeTokenHash(tokenSecret);
                if (!FixedTimeEquals(expectedHash, presentedHash))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ConfirmEmailChangeResult(ConfirmEmailChangeOutcome.InvalidToken, "Confirmation token hash mismatch.");
                }

                var consumed = await TryConsumeEmailChangeTokenAsync(tokenId, now, cancellationToken);
                if (!consumed)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ConfirmEmailChangeResult(ConfirmEmailChangeOutcome.ExpiredOrUsedToken, "Confirmation token is expired or already used.");
                }

                var user = await dbContext.Users
                    .FirstOrDefaultAsync(existingUser => existingUser.Id == token.UserId, cancellationToken);

                if (user is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ConfirmEmailChangeResult(ConfirmEmailChangeOutcome.InvalidToken, "Token user could not be found.");
                }

                var targetEmailInUse = await dbContext.Users
                    .AnyAsync(existingUser => existingUser.Id != user.Id && existingUser.Email == token.NewEmail, cancellationToken);

                if (targetEmailInUse)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new ConfirmEmailChangeResult(ConfirmEmailChangeOutcome.TargetEmailUnavailable, "Requested email is unavailable.");
                }

                var previousEmail = user.Email;
                user.Email = token.NewEmail;
                user.ModifiedAtUtc = now;

                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return new ConfirmEmailChangeResult(
                    ConfirmEmailChangeOutcome.Success,
                    null,
                    user.Id,
                    previousEmail,
                    token.NewEmail);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> TryConsumeEmailChangeTokenAsync(Guid tokenId, DateTime now, CancellationToken cancellationToken)
    {
        try
        {
            var consumedCount = await dbContext.EmailChangeTokens
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
            var trackedToken = await dbContext.EmailChangeTokens
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

    private static bool TryParseConfirmationToken(string confirmationToken, out Guid tokenId, out string tokenSecret)
    {
        tokenId = Guid.Empty;
        tokenSecret = string.Empty;

        if (string.IsNullOrWhiteSpace(confirmationToken))
        {
            return false;
        }

        var parts = confirmationToken.Split('.', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out tokenId))
        {
            return false;
        }

        tokenSecret = parts[1];
        return !string.IsNullOrWhiteSpace(tokenSecret);
    }
}
