using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Features.Auth.Security;
using TaskTracker.Api.Features.Integrations.Authentication;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Integrations.Services;

public sealed class IntegrationCredentialService(
    TaskTrackerDbContext dbContext,
    IPasswordHasher passwordHasher) : IIntegrationCredentialService
{
    public async Task<IntegrationCredentialIssueResult> IssueAsync(
        Guid ownerUserId,
        string integrationId,
        string integrationName,
        IReadOnlyCollection<string> scopes,
        DateTime? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var normalizedScopes = scopes
            .Select(IntegrationScopes.Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var now = DateTime.UtcNow;
        var secret = WebEncoders.Base64UrlEncode(Guid.NewGuid().ToByteArray())
            + WebEncoders.Base64UrlEncode(Guid.NewGuid().ToByteArray());

        var keyId = $"itg_{Guid.NewGuid():N}";
        var (hash, salt) = passwordHasher.HashPassword(secret);

        var credential = new IntegrationCredential
        {
            Id = Guid.NewGuid(),
            KeyId = keyId,
            IntegrationId = integrationId,
            IntegrationName = integrationName,
            OwnerUserId = ownerUserId,
            SecretHash = hash,
            SecretSalt = salt,
            Status = IntegrationCredentialStatus.Active,
            ExpiresAtUtc = expiresAtUtc,
            CreatedAtUtc = now
        };

        foreach (var scope in normalizedScopes)
        {
            credential.Scopes.Add(new IntegrationCredentialScope
            {
                Id = Guid.NewGuid(),
                Scope = scope,
                CreatedAtUtc = now
            });
        }

        dbContext.IntegrationCredentials.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new IntegrationCredentialIssueResult(credential, secret, normalizedScopes);
    }

    public async Task<IReadOnlyCollection<IntegrationCredentialView>> ListOwnedAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var credentials = await dbContext.IntegrationCredentials
            .AsNoTracking()
            .Where(item => item.OwnerUserId == ownerUserId)
            .Include(item => item.Scopes)
            .OrderByDescending(item => item.CreatedAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return credentials
            .Select(item => new IntegrationCredentialView(
                item,
                item.Scopes.Select(scope => scope.Scope).OrderBy(scope => scope, StringComparer.Ordinal).ToArray()))
            .ToArray();
    }

    public async Task<IntegrationCredentialRevocationResult> RevokeOwnedAsync(
        Guid ownerUserId,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        var credential = await dbContext.IntegrationCredentials
            .FirstOrDefaultAsync(
                item => item.Id == credentialId && item.OwnerUserId == ownerUserId,
                cancellationToken);

        if (credential is null)
        {
            return new IntegrationCredentialRevocationResult(IntegrationCredentialRevocationStatus.NotFound, null);
        }

        if (credential.Status != IntegrationCredentialStatus.Revoked)
        {
            credential.Status = IntegrationCredentialStatus.Revoked;
            credential.RevokedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new IntegrationCredentialRevocationResult(IntegrationCredentialRevocationStatus.Revoked, credential);
    }
}
