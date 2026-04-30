using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Features.Leaderboards.Contracts;
using TaskTracker.Api.Features.SharedViews.Caching;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Leaderboards.Repositories;

public class LeaderboardRepository(
    TaskTrackerDbContext dbContext,
    ISharedViewCacheCoordinator sharedViewCache,
    ILogger<LeaderboardRepository> logger) : ILeaderboardRepository
{
    public async Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<LeaderboardPage> GetLeaderboardAsync(
        LeaderboardType type,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        try
        {
            return await sharedViewCache.GetOrCreateLeaderboardAsync(
                type,
                page,
                pageSize,
                ct => QueryLeaderboardAsync(type, page, pageSize, ct),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Shared view cache failed for leaderboard read. Falling back to SQL query. Type: {Type}. Page: {Page}. PageSize: {PageSize}.",
                type,
                page,
                pageSize);

            return await QueryLeaderboardAsync(type, page, pageSize, cancellationToken);
        }
    }

    private Task<LeaderboardPage> QueryLeaderboardAsync(
        LeaderboardType type,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        return type switch
        {
            LeaderboardType.Streak => GetStreakLeaderboardAsync(page, pageSize, cancellationToken),
            LeaderboardType.CompletedTasks => GetCompletedTasksLeaderboardAsync(page, pageSize, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported leaderboard type.")
        };
    }

    private async Task<LeaderboardPage> GetStreakLeaderboardAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var metrics =
            from user in dbContext.Users.AsNoTracking()
            where user.LeaderboardParticipationMode != LeaderboardParticipationMode.Hidden
            join snapshot in dbContext.UserStreakSnapshots.AsNoTracking()
                on user.Id equals snapshot.OwnerId into snapshots
            from snapshot in snapshots.DefaultIfEmpty()
            select new LeaderboardMetric(
                user.Id,
                user.DisplayName,
                user.LeaderboardParticipationMode,
                snapshot != null ? snapshot.CurrentStreakDays : 0);

        return await BuildLeaderboardPageAsync(
            LeaderboardType.Streak,
            metrics,
            page,
            pageSize,
            cancellationToken);
    }

    private async Task<LeaderboardPage> GetCompletedTasksLeaderboardAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var completedCounts = dbContext.TaskCompletionEvents
            .AsNoTracking()
            .Where(completionEvent => completionEvent.EventName == "TaskCompleted")
            .GroupBy(completionEvent => completionEvent.OwnerId)
            .Select(group => new
            {
                UserId = group.Key,
                CompletedCount = group.Count()
            });

        var metrics =
            from user in dbContext.Users.AsNoTracking()
            where user.LeaderboardParticipationMode != LeaderboardParticipationMode.Hidden
            join completedCount in completedCounts on user.Id equals completedCount.UserId into completedCountsByUser
            from completedCount in completedCountsByUser.DefaultIfEmpty()
            select new LeaderboardMetric(
                user.Id,
                user.DisplayName,
                user.LeaderboardParticipationMode,
                completedCount != null ? completedCount.CompletedCount : 0);

        return await BuildLeaderboardPageAsync(
            LeaderboardType.CompletedTasks,
            metrics,
            page,
            pageSize,
            cancellationToken);
    }

    private static async Task<LeaderboardPage> BuildLeaderboardPageAsync(
        LeaderboardType type,
        IQueryable<LeaderboardMetric> metrics,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var orderedMetrics = metrics
            .OrderByDescending(item => item.MetricValue)
            .ThenBy(item => item.UserId);

        var totalCount = await orderedMetrics.CountAsync(cancellationToken);
        var offset = (page - 1) * pageSize;

        var pageItems = await orderedMetrics
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var entries = pageItems
            .Select((item, index) =>
            {
                var identity = ResolveIdentity(item);

                return new LeaderboardEntry(
                    offset + index + 1,
                    identity.PublicIdentity,
                    identity.IdentityMode,
                    ResolveAvatarMarker(item.UserId),
                    item.MetricValue);
            })
            .ToArray();

        return new LeaderboardPage(type, page, pageSize, totalCount, entries);
    }

    private static ResolvedIdentity ResolveIdentity(LeaderboardMetric metric)
    {
        if (metric.ParticipationMode == LeaderboardParticipationMode.Public && !string.IsNullOrWhiteSpace(metric.DisplayName))
        {
            return new ResolvedIdentity(metric.DisplayName.Trim(), LeaderboardIdentityMode.Public);
        }

        return new ResolvedIdentity($"anon-{metric.UserId:N}"[..13], LeaderboardIdentityMode.Anonymous);
    }

    private static string ResolveAvatarMarker(Guid userId)
    {
        var source = Encoding.UTF8.GetBytes($"avatar-marker-v1:{userId:N}");
        var digest = SHA256.HashData(source);
        return $"avatar-{Convert.ToHexString(digest[..6]).ToLowerInvariant()}";
    }

    private sealed record ResolvedIdentity(string PublicIdentity, LeaderboardIdentityMode IdentityMode);

    private sealed record LeaderboardMetric(
        Guid UserId,
        string DisplayName,
        LeaderboardParticipationMode ParticipationMode,
        int MetricValue);
}