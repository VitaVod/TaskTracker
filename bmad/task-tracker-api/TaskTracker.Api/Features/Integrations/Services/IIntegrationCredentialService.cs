using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Integrations.Services;

public interface IIntegrationCredentialService
{
    Task<IntegrationCredentialIssueResult> IssueAsync(
        Guid ownerUserId,
        string integrationId,
        string integrationName,
        IReadOnlyCollection<string> scopes,
        DateTime? expiresAtUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<IntegrationCredentialView>> ListOwnedAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken);

    Task<IntegrationCredentialRevocationResult> RevokeOwnedAsync(
        Guid ownerUserId,
        Guid credentialId,
        CancellationToken cancellationToken);
}

public sealed record IntegrationCredentialIssueResult(
    IntegrationCredential Credential,
    string PlainTextSecret,
    IReadOnlyCollection<string> Scopes);

public sealed record IntegrationCredentialView(
    IntegrationCredential Credential,
    IReadOnlyCollection<string> Scopes);

public enum IntegrationCredentialRevocationStatus
{
    NotFound,
    Revoked
}

public sealed record IntegrationCredentialRevocationResult(
    IntegrationCredentialRevocationStatus Status,
    IntegrationCredential? Credential);
