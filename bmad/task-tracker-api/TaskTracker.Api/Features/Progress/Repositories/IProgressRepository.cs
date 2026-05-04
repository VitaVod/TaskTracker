using TaskTracker.Api.Features.Progress.Contracts;
using TaskTracker.Api.Features.Tasks.Contracts;

namespace TaskTracker.Api.Features.Progress.Repositories;

public sealed record ProgressXpSummary(
    int TotalXp,
    int LedgerEntryCount,
    DateTime? LastGrantedAtUtc,
    ProgressLevelSnapshot LevelProgress,
    string OutcomeReasonCode,
    string OutcomeExplanation);

public sealed record ProgressLevelSnapshot(
    int CurrentLevel,
    int CurrentLevelThresholdXp,
    int NextLevel,
    int NextLevelThresholdXp,
    double PercentToNextLevel,
    IReadOnlyCollection<int> BandMilestoneLevels,
    int ReachedBandCount,
    int? NextBandLevel);

public sealed record ProgressStreakSnapshot(
    TaskStreakOutcome Outcome,
    int CurrentStreakDays,
    int LongestStreakDays,
    string TimeZoneId,
    DateTime EvaluationWindowStartUtc,
    DateTime EvaluationWindowEndUtc,
    DateTime LastEvaluatedAtUtc,
    bool IsRecoveryPromptVisible,
    string? RecoveryReason,
    string? RecommendedAction,
    string OutcomeReasonCode,
    string OutcomeExplanation,
    string? RecoveryExplanation);

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
