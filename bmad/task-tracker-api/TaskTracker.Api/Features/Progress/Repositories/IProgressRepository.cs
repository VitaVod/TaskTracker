using TaskTracker.Api.Features.Progress.Contracts;
using TaskTracker.Api.Features.Tasks.Contracts;

namespace TaskTracker.Api.Features.Progress.Repositories;

public sealed record ProgressXpSummary(
    int TotalXp,
    int LedgerEntryCount,
    DateTime? LastGrantedAtUtc);

public sealed record ProgressStreakSnapshot(
    TaskStreakOutcome Outcome,
    int CurrentStreakDays,
    int LongestStreakDays,
    string TimeZoneId,
    DateTime EvaluationWindowStartUtc,
    DateTime EvaluationWindowEndUtc,
    DateTime LastEvaluatedAtUtc);

public sealed record ProgressTrendPoint(
    DateTime BucketStartUtc,
    DateTime BucketEndUtc,
    int CompletedTaskCount,
    int XpGranted);

public sealed record ProgressTrendSummary(
    string TimeZoneId,
    DateTime RangeStartUtc,
    DateTime RangeEndUtc,
    IReadOnlyCollection<ProgressTrendPoint> Items);

public interface IProgressRepository
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken);

    Task<ProgressXpSummary> GetXpSummaryAsync(Guid userId, CancellationToken cancellationToken);

    Task<ProgressStreakSnapshot> GetStreakSnapshotAsync(Guid userId, CancellationToken cancellationToken);

    Task<ProgressTrendSummary> GetTrendSummaryAsync(
        Guid userId,
        ProgressTrendGranularity granularity,
        int windowDays,
        DateTime nowUtc,
        CancellationToken cancellationToken);
}
