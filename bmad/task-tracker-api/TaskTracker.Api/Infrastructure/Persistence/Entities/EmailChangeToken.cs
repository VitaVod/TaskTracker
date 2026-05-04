namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class EmailChangeToken
{
    public Guid TokenId { get; set; }

    public Guid UserId { get; set; }

    public string NewEmail { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime RequestedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}