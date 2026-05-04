namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class IntegrationCredential
{
    public Guid Id { get; set; }

    public string KeyId { get; set; } = string.Empty;

    public string IntegrationId { get; set; } = string.Empty;

    public string IntegrationName { get; set; } = string.Empty;

    public Guid OwnerUserId { get; set; }

    public string SecretHash { get; set; } = string.Empty;

    public string SecretSalt { get; set; } = string.Empty;

    public IntegrationCredentialStatus Status { get; set; } = IntegrationCredentialStatus.Active;

    public DateTime? ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? RotatedAtUtc { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }

    public ICollection<IntegrationCredentialScope> Scopes { get; set; } = [];
}
