namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class PasswordRecoveryToken
{
    public Guid TokenId { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? UsedAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public int DeliveryAttemptCount { get; set; }

    public DateTime? LastDeliveryAttemptAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
