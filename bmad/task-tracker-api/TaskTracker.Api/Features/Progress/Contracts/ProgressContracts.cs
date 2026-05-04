namespace TaskTracker.Api.Features.Progress.Contracts;

public enum ProgressTrendGranularity
{
    Daily,
    Weekly
}

public record ProgressTrendQuery(string? Granularity, int? WindowDays);

public record ProgressExplanationResponse(
    string ReasonCode,
    string Message);

public record ProgressLevelSnapshotResponse(
    int CurrentLevel,
    int CurrentLevelThresholdXp,
    int NextLevel,
    int NextLevelThresholdXp,
    double PercentToNextLevel,
    IReadOnlyCollection<int> BandMilestoneLevels,
    int ReachedBandCount,
    int? NextBandLevel);

public record ProgressXpSummaryResponse(
    int TotalXp,
    int LedgerEntryCount,
    DateTime? LastGrantedAtUtc,
    ProgressLevelSnapshotResponse LevelProgress,
    ProgressExplanationResponse OutcomeExplanation);

public record ProgressStreakSnapshotResponse(
    TaskTracker.Api.Features.Tasks.Contracts.TaskStreakOutcome Outcome,
    int CurrentStreakDays,
    int LongestStreakDays,
    string TimeZoneId,
    DateTime EvaluationWindowStartUtc,
    DateTime EvaluationWindowEndUtc,
    DateTime LastEvaluatedAtUtc,
    bool IsRecoveryPromptVisible,
    string? RecoveryReason,
    string? RecommendedAction,
    ProgressExplanationResponse OutcomeExplanation,
    ProgressExplanationResponse? RecoveryExplanation);

public record ProgressTrendPointResponse(
    DateTime BucketStartUtc,
    DateTime BucketEndUtc,
    int CompletedTaskCount,
    int XpGranted);

public record ProgressTrendSummaryResponse(
    string Granularity,
    int WindowDays,
    string TimeZoneId,
    DateTime RangeStartUtc,
    DateTime RangeEndUtc,
    IReadOnlyCollection<ProgressTrendPointResponse> Items);
