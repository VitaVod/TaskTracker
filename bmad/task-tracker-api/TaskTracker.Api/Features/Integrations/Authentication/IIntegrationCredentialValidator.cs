using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Integrations.Authentication;

public interface IIntegrationCredentialValidator
{
    Task<IntegrationCredentialValidationResult> ValidateAsync(
        string keyId,
        string secret,
        CancellationToken cancellationToken);
}

public enum IntegrationCredentialValidationStatus
{
    Success,
    Invalid,
    Revoked,
    Expired
}

public sealed record IntegrationCredentialValidationResult(
    IntegrationCredentialValidationStatus Status,
    IntegrationCredential? Credential,
    IReadOnlyCollection<string> Scopes);
