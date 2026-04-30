namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class XpLedgerEntry
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public Guid TaskId { get; set; }

    public Guid TaskCompletionEventId { get; set; }

    public string EventName { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public int XpGranted { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
