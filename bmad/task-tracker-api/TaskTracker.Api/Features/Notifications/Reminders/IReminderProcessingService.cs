using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Notifications.Reminders;

public interface IReminderProcessingService
{
    Task<ReminderProcessingRunResult> ProcessAsync(string traceId, CancellationToken cancellationToken);
}

public sealed record ReminderProcessingRunResult(
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    int EligibleUserCount,
    int ProcessedUserCount,
    int SentCount,
    int SkippedCount,
    int FailedCount);

internal sealed record ReminderCadenceWindow(
    NotificationReminderCadence Cadence,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc);
