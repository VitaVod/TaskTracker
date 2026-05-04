namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class IntegrationProcessingFailureEvent
{
    public Guid Id { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string IntegrationId { get; set; } = string.Empty;

    public Guid OwnerUserId { get; set; }

    public string? ExternalTaskId { get; set; }

    public string? IdempotencyKey { get; set; }

    public string ErrorClass { get; set; } = string.Empty;

    public string ErrorCode { get; set; } = string.Empty;

    public int HttpStatus { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;
}
