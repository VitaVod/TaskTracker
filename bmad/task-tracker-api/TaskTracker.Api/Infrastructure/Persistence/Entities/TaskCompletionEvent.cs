namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class TaskCompletionEvent
{
    public Guid Id { get; set; }

    public Guid TaskId { get; set; }

    public Guid OwnerId { get; set; }

    public string EventName { get; set; } = string.Empty;

    public bool ResultingIsCompleted { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
