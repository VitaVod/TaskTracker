namespace TaskTracker.Api.Features.Progress.Contracts;

public enum ProgressTrendGranularity
{
    Daily,
    Weekly
}

public record ProgressTrendQuery(string? Granularity, int? WindowDays);

public record ProgressXpSummaryResponse(
    int TotalXp,
    int LedgerEntryCount,
    DateTime? LastGrantedAtUtc);

public record ProgressStreakSnapshotResponse(
    TaskTracker.Api.Features.Tasks.Contracts.TaskStreakOutcome Outcome,
    int CurrentStreakDays,
    int LongestStreakDays,
    string TimeZoneId,
    DateTime EvaluationWindowStartUtc,
    DateTime EvaluationWindowEndUtc,
    DateTime LastEvaluatedAtUtc);

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
