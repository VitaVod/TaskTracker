namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class PrivilegedActionAudit
{
    public Guid Id { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    public Guid? TargetUserId { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string ReasonCode { get; set; } = string.Empty;

    public string ReasonText { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public DateTime OccurredAtUtc { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;

    public string IntentKey { get; set; } = string.Empty;
}