namespace TaskTracker.Api.Features.Notifications.Contracts;

public sealed record ReminderProcessingResponse(
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    int EligibleUserCount,
    int ProcessedUserCount,
    int SentCount,
    int SkippedCount,
    int FailedCount);
