using TaskTracker.Api.Features.Leaderboards.Contracts;
using TaskTracker.Api.Features.Leaderboards.Repositories;

namespace TaskTracker.Api.Features.SharedViews.Caching;

public interface ISharedViewCacheCoordinator
{
    Task<LeaderboardPage> GetOrCreateLeaderboardAsync(
        LeaderboardType type,
        int page,
        int pageSize,
        Func<CancellationToken, Task<LeaderboardPage>> factory,
        CancellationToken cancellationToken);

    Task<(long TotalTasksCreated, long TotalTasksCompleted)> GetOrCreateGlobalStatisticsAsync(
        Func<CancellationToken, Task<(long TotalTasksCreated, long TotalTasksCompleted)>> factory,
        CancellationToken cancellationToken);

    Task InvalidateAfterCompletionCommitAsync(
        string idempotencyKey,
        string traceId,
        CancellationToken cancellationToken);
}
