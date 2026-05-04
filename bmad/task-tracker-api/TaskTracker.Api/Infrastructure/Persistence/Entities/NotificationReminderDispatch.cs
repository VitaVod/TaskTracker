namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class NotificationReminderDispatch
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public NotificationReminderCadence Cadence { get; set; }

    public DateTime WindowStartUtc { get; set; }

    public DateTime WindowEndUtc { get; set; }

    public NotificationReminderDispatchStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public int TaskCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public string TraceId { get; set; } = string.Empty;
}