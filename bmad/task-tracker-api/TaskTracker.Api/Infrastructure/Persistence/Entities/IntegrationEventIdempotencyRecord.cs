namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class IntegrationEventIdempotencyRecord
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string IntegrationId { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public Guid TaskId { get; set; }

    public string ExternalTaskId { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public DateTime ProcessedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

