namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class ModerationActionAudit
{
    public Guid Id { get; set; }

    public string CaseId { get; set; } = string.Empty;

    public string CorrelationRef { get; set; } = string.Empty;

    public Guid TargetUserId { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    public string ActionType { get; set; } = string.Empty;

    public string ReasonCode { get; set; } = string.Empty;

    public string ReasonText { get; set; } = string.Empty;

    public bool ConfirmDestructive { get; set; }

    public string? ConfirmationToken { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public string IntentKey { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public string TraceId { get; set; } = string.Empty;
}
