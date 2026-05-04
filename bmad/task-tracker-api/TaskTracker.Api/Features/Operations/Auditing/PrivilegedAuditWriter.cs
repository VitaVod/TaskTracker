using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Operations.Auditing;

public sealed class PrivilegedAuditWriter(TaskTrackerDbContext dbContext) : IPrivilegedAuditWriter
{
    public async Task<PrivilegedAuditWriteResult> AppendAsync(PrivilegedAuditWriteRequest request, CancellationToken cancellationToken)
    {
        var existing = await dbContext.PrivilegedActionAudits
            .AsNoTracking()
            .FirstOrDefaultAsync(audit => audit.IntentKey == request.IntentKey, cancellationToken);

        if (existing is not null)
        {
            return new PrivilegedAuditWriteResult(existing.Id, existing.OccurredAtUtc, existing.Outcome, true);
        }

        var audit = new PrivilegedActionAudit
        {
            Id = Guid.NewGuid(),
            ActorUserId = request.ActorUserId,
            ActorRole = request.ActorRole,
            TargetUserId = request.TargetUserId,
            ActionType = request.ActionType,
            ReasonCode = request.ReasonCode,
            ReasonText = request.ReasonText,
            Outcome = request.Outcome,
            OccurredAtUtc = request.OccurredAtUtc,
            CorrelationId = request.CorrelationId,
            TraceId = request.TraceId,
            IntentKey = request.IntentKey
        };

        dbContext.PrivilegedActionAudits.Add(audit);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new PrivilegedAuditWriteResult(audit.Id, audit.OccurredAtUtc, audit.Outcome, false);
        }
        catch (DbUpdateException)
        {
            var conflict = await dbContext.PrivilegedActionAudits
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.IntentKey == request.IntentKey, cancellationToken);

            if (conflict is not null)
            {
                return new PrivilegedAuditWriteResult(conflict.Id, conflict.OccurredAtUtc, conflict.Outcome, true);
            }

            throw;
        }
    }
}