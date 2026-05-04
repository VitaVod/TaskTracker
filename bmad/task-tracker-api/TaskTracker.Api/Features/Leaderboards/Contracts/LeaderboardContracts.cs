namespace TaskTracker.Api.Features.Leaderboards.Contracts;

public enum LeaderboardType
{
    Streak,
    CompletedTasks
}

public record LeaderboardQuery(string? Type, int? Page, int? PageSize);

public enum LeaderboardIdentityMode
{
    Public,
    Anonymous
}

public record LeaderboardEntryResponse(
    int Rank,
    string PublicIdentity,
    string IdentityMode,
    string AvatarMarker,
    int MetricValue,
    string? PublicProfileHandle);

public record PublicProfileStatisticsResponse(
    int CurrentStreakDays,
    int LongestStreakDays,
    int CompletedTaskCount,
    int TotalXp,
    DateTime? LastCompletedAtUtc);

public record PublicProfileResponse(
    string Visibility,
    string? PublicIdentity,
    string? AvatarMarker,
    PublicProfileStatisticsResponse? Statistics,
    string? Message);

public record LeaderboardResponse(
    string Type,
    int Page,
    int PageSize,
    int TotalCount,
    bool HasNextPage,
    IReadOnlyCollection<LeaderboardEntryResponse> Items);