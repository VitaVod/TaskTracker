namespace TaskTracker.Api.Features.Integrations.Contracts;

public sealed record CreateIntegrationCredentialRequest(
    string? IntegrationId,
    string? IntegrationName,
    string[]? Scopes,
    DateTime? ExpiresAtUtc);

public sealed record IntegrationCredentialCreatedResponse(
    Guid CredentialId,
    string KeyId,
    string IntegrationId,
    string IntegrationName,
    Guid OwnerUserId,
    string[] Scopes,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    string Secret,
    string TraceId);

public sealed record IntegrationCredentialListItemResponse(
    Guid CredentialId,
    string KeyId,
    string IntegrationId,
    string IntegrationName,
    Guid OwnerUserId,
    string Status,
    string[] Scopes,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    DateTime? RotatedAtUtc,
    DateTime? LastUsedAtUtc);

public sealed record IntegrationCredentialListResponse(IntegrationCredentialListItemResponse[] Credentials);

public sealed record IntegrationCredentialRevokedResponse(
    Guid CredentialId,
    string Status,
    DateTime RevokedAtUtc,
    string TraceId);

public sealed record IntegrationTaskCreateSyncRequest(
    string? ExternalTaskId,
    string? Title,
    string? Description,
    DateTime? DueAtUtc,
    string? Priority,
    string? Category,
    bool? IsCompleted,
    string? Difficulty = null,
    string? EnergyLevel = null,
    string? ContextTag = null,
    int? EffortPoints = null);

public sealed record IntegrationTaskCreateSyncResponse(
    string Operation,
    bool IdempotentReplay,
    string IntegrationId,
    Guid OwnerUserId,
    Guid TaskId,
    string ExternalTaskId,
    string? ErrorClass,
    string? RecoveryHint,
    string CorrelationId,
    string TraceId);
