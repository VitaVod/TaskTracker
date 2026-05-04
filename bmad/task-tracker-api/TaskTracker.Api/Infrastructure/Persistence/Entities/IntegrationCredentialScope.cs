namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class IntegrationCredentialScope
{
    public Guid Id { get; set; }

    public Guid CredentialId { get; set; }

    public string Scope { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public IntegrationCredential Credential { get; set; } = null!;
}
