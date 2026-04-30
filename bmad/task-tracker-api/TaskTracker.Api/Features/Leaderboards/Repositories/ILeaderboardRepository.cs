using TaskTracker.Api.Features.Leaderboards.Contracts;

namespace TaskTracker.Api.Features.Leaderboards.Repositories;

public sealed record LeaderboardEntry(
    int Rank,
    string PublicIdentity,
    LeaderboardIdentityMode IdentityMode,
    string AvatarMarker,
    int MetricValue);

public sealed record LeaderboardPage(
    LeaderboardType Type,
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyCollection<LeaderboardEntry> Items);

public interface ILeaderboardRepository
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken);

    Task<LeaderboardPage> GetLeaderboardAsync(
        LeaderboardType type,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}