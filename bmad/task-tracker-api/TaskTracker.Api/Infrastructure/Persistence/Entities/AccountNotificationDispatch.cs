namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class AccountNotificationDispatch
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string EventKey { get; set; } = string.Empty;

    public AccountNotificationEventType EventType { get; set; }

    public string ToEmail { get; set; } = string.Empty;

    public AccountNotificationDispatchStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastUpdatedAtUtc { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public string TraceId { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public string? LastFailureCategory { get; set; }
}
