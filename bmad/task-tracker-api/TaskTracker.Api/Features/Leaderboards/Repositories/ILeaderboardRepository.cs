using TaskTracker.Api.Features.Leaderboards.Contracts;

namespace TaskTracker.Api.Features.Leaderboards.Repositories;

public sealed record LeaderboardEntry(
    int Rank,
    string PublicIdentity,
    LeaderboardIdentityMode IdentityMode,
    string AvatarMarker,
    int MetricValue,
    string? PublicProfileHandle);

public sealed record PublicProfileReadModel(
    string PublicIdentity,
    string AvatarMarker,
    int CurrentStreakDays,
    int LongestStreakDays,
    int CompletedTaskCount,
    int TotalXp,
    DateTime? LastCompletedAtUtc);

public sealed record LeaderboardPage(
    LeaderboardType Type,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyCollection<LeaderboardEntry> Items);

public sealed record SuspiciousActivityCase(
    string CaseId,
    string PublicIdentity,
    LeaderboardIdentityMode IdentityMode,
    string AnomalyType,
    string SignalSummary,
    int Severity,
    DateTime DetectedAtUtc,
    DateTime? LastActivityAtUtc,
    string CorrelationRef);

public sealed record SuspiciousActivityCasePage(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyCollection<SuspiciousActivityCase> Items);

public interface ILeaderboardRepository
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken);

    Task<LeaderboardPage> GetLeaderboardAsync(
        LeaderboardType type,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PublicProfileReadModel?> GetPublicProfileAsync(
        string profileHandle,
        CancellationToken cancellationToken);

    Task<SuspiciousActivityCasePage> GetSuspiciousActivityCasesAsync(
        string? anomalyType,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<SuspiciousActivityCase?> GetSuspiciousActivityCaseByIdAsync(
        string caseId,
        CancellationToken cancellationToken);
}