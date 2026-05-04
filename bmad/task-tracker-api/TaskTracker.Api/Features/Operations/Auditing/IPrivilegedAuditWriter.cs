namespace TaskTracker.Api.Features.Operations.Auditing;

public interface IPrivilegedAuditWriter
{
    Task<PrivilegedAuditWriteResult> AppendAsync(PrivilegedAuditWriteRequest request, CancellationToken cancellationToken);
}

public sealed record PrivilegedAuditWriteRequest(
    string ActorUserId,
    string ActorRole,
    Guid? TargetUserId,
    string ActionType,
    string ReasonCode,
    string ReasonText,
    string Outcome,
    DateTime OccurredAtUtc,
    string CorrelationId,
    string TraceId,
    string IntentKey);

public sealed record PrivilegedAuditWriteResult(
    Guid AuditId,
    DateTime OccurredAtUtc,
    string Outcome,
    bool AlreadyExists);