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
    private const int ActivitySpikeThreshold = 5;
    private const int RankingMismatchCompletedThreshold = 10;

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

    public async Task<PublicProfileReadModel?> GetPublicProfileAsync(
        string profileHandle,
        CancellationToken cancellationToken)
    {
        if (!TryParsePublicProfileHandle(profileHandle, out var userId))
        {
            return null;
        }

        var candidate = await (
            from user in dbContext.Users.AsNoTracking()
            where user.Id == userId
                && user.Role == "User"
                && user.LeaderboardParticipationMode == LeaderboardParticipationMode.Public
            let currentStreakDays = dbContext.UserStreakSnapshots
                .AsNoTracking()
                .Where(snapshot => snapshot.OwnerId == user.Id)
                .Select(snapshot => (int?)snapshot.CurrentStreakDays)
                .FirstOrDefault()
            let longestStreakDays = dbContext.UserStreakSnapshots
                .AsNoTracking()
                .Where(snapshot => snapshot.OwnerId == user.Id)
                .Select(snapshot => (int?)snapshot.LongestStreakDays)
                .FirstOrDefault()
            let completedTaskCount = dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == user.Id && task.IsCompleted)
                .Count()
            let lastCompletedAtUtc = dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == user.Id && task.IsCompleted)
                .Select(task => (DateTime?)task.UpdatedAtUtc)
                .Max()
            let totalXp = dbContext.XpLedgerEntries
                .AsNoTracking()
                .Where(entry => entry.OwnerId == user.Id)
                .Select(entry => (int?)entry.XpGranted)
                .Sum()
            select new
            {
                user.Id,
                user.DisplayName,
                CurrentStreakDays = currentStreakDays ?? 0,
                LongestStreakDays = longestStreakDays ?? 0,
                CompletedTaskCount = completedTaskCount,
                LastCompletedAtUtc = lastCompletedAtUtc,
                TotalXp = totalXp ?? 0
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate is null)
        {
            return null;
        }

        var identity = ResolveIdentity(new LeaderboardMetric(
            candidate.Id,
            candidate.DisplayName,
            LeaderboardParticipationMode.Public,
            candidate.CurrentStreakDays));

        if (identity.IdentityMode != LeaderboardIdentityMode.Public)
        {
            return null;
        }

        return new PublicProfileReadModel(
            identity.PublicIdentity,
            ResolveAvatarMarker(candidate.Id),
            candidate.CurrentStreakDays,
            candidate.LongestStreakDays,
            candidate.CompletedTaskCount,
            candidate.TotalXp,
            candidate.LastCompletedAtUtc);
    }

    public async Task<SuspiciousActivityCasePage> GetSuspiciousActivityCasesAsync(
        string? anomalyType,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedAnomalyType = NormalizeAnomalyType(anomalyType);
        var now = DateTime.UtcNow;
        var recentWindowStartUtc = now.AddDays(-7);

        var userSignals =
            from user in dbContext.Users.AsNoTracking()
            where user.Role == "User"
            let completedCount = dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == user.Id && task.IsCompleted)
                .Count()
            let recentCompletedCount = dbContext.Tasks
                .AsNoTracking()
                .Where(task =>
                    task.UserId == user.Id
                    && task.IsCompleted
                    && task.UpdatedAtUtc >= recentWindowStartUtc)
                .Count()
            let latestCompletedAtUtc = dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == user.Id && task.IsCompleted)
                .Select(task => (DateTime?)task.UpdatedAtUtc)
                .Max()
            let currentStreakDays = dbContext.UserStreakSnapshots
                .AsNoTracking()
                .Where(snapshot => snapshot.OwnerId == user.Id)
                .Select(snapshot => (int?)snapshot.CurrentStreakDays)
                .FirstOrDefault()
            select new
            {
                user.Id,
                user.DisplayName,
                user.LeaderboardParticipationMode,
                CompletedCount = completedCount,
                RecentCompletedCount = recentCompletedCount,
                CurrentStreakDays = currentStreakDays ?? 0,
                LatestCompletedAtUtc = latestCompletedAtUtc
            };

        var signalRows = await userSignals
            .Select(signal => new UserAnomalySignal(
                signal.Id,
                signal.DisplayName,
                signal.LeaderboardParticipationMode,
                signal.CompletedCount,
                signal.RecentCompletedCount,
                signal.CurrentStreakDays,
                signal.LatestCompletedAtUtc))
            .ToListAsync(cancellationToken);
        var scoredCases = signalRows
            .SelectMany(signal => BuildSuspiciousCases(signal, normalizedAnomalyType, now))
            .ToList();

        var orderedCases = scoredCases
            .OrderByDescending(item => item.Severity)
            .ThenByDescending(item => item.DetectedAtUtc)
            .ThenBy(item => item.CaseId, StringComparer.Ordinal)
            .ToList();

        var totalCount = orderedCases.Count;
        var offset = (page - 1) * pageSize;
        var pageItems = orderedCases
            .Skip(offset)
            .Take(pageSize)
            .ToArray();

        return new SuspiciousActivityCasePage(page, pageSize, totalCount, pageItems);
    }

    public async Task<SuspiciousActivityCase?> GetSuspiciousActivityCaseByIdAsync(
        string caseId,
        CancellationToken cancellationToken)
    {
        if (!TryParseCaseIdentity(caseId, out var anomalyType, out var userId))
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var recentWindowStartUtc = now.AddDays(-7);

        var signal = await (
            from user in dbContext.Users.AsNoTracking()
            where user.Id == userId && user.Role == "User"
            let completedCount = dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == user.Id && task.IsCompleted)
                .Count()
            let recentCompletedCount = dbContext.Tasks
                .AsNoTracking()
                .Where(task =>
                    task.UserId == user.Id
                    && task.IsCompleted
                    && task.UpdatedAtUtc >= recentWindowStartUtc)
                .Count()
            let latestCompletedAtUtc = dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == user.Id && task.IsCompleted)
                .Select(task => (DateTime?)task.UpdatedAtUtc)
                .Max()
            let currentStreakDays = dbContext.UserStreakSnapshots
                .AsNoTracking()
                .Where(snapshot => snapshot.OwnerId == user.Id)
                .Select(snapshot => (int?)snapshot.CurrentStreakDays)
                .FirstOrDefault()
            select new UserAnomalySignal(
                user.Id,
                user.DisplayName,
                user.LeaderboardParticipationMode,
                completedCount,
                recentCompletedCount,
                currentStreakDays ?? 0,
                latestCompletedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);

        if (signal is null)
        {
            return null;
        }

        return BuildSuspiciousCases(signal, anomalyType, now)
            .SingleOrDefault(item => string.Equals(item.CaseId, caseId, StringComparison.OrdinalIgnoreCase));
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
            where user.LeaderboardParticipationMode == LeaderboardParticipationMode.Public
                || user.LeaderboardParticipationMode == LeaderboardParticipationMode.Anonymous
            let currentStreakDays = dbContext.UserStreakSnapshots
                .AsNoTracking()
                .Where(snapshot => snapshot.OwnerId == user.Id)
                .Select(snapshot => (int?)snapshot.CurrentStreakDays)
                .FirstOrDefault()
            select new
            {
                user.Id,
                user.DisplayName,
                user.LeaderboardParticipationMode,
                MetricValue = currentStreakDays ?? 0
            };

        var orderedMetrics = metrics
            .OrderByDescending(item => item.MetricValue)
            .ThenBy(item => item.Id);

        var totalCount = await orderedMetrics.CountAsync(cancellationToken);
        var offset = (page - 1) * pageSize;

        var pageItems = await orderedMetrics
            .Skip(offset)
            .Take(pageSize)
            .Select(item => new LeaderboardMetric(
                item.Id,
                item.DisplayName,
                item.LeaderboardParticipationMode,
                item.MetricValue))
            .ToListAsync(cancellationToken);

        return BuildLeaderboardPage(
            LeaderboardType.Streak,
            pageItems,
            page,
            pageSize,
            totalCount);
    }

    private async Task<LeaderboardPage> GetCompletedTasksLeaderboardAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var metrics =
            from user in dbContext.Users.AsNoTracking()
            where user.LeaderboardParticipationMode == LeaderboardParticipationMode.Public
                || user.LeaderboardParticipationMode == LeaderboardParticipationMode.Anonymous
            let completedCount = dbContext.Tasks
                .AsNoTracking()
                .Where(task => task.UserId == user.Id && task.IsCompleted)
                .Count()
            select new
            {
                user.Id,
                user.DisplayName,
                user.LeaderboardParticipationMode,
                MetricValue = completedCount
            };

        var orderedMetrics = metrics
            .OrderByDescending(item => item.MetricValue)
            .ThenBy(item => item.Id);

        var totalCount = await orderedMetrics.CountAsync(cancellationToken);
        var offset = (page - 1) * pageSize;

        var pageItems = await orderedMetrics
            .Skip(offset)
            .Take(pageSize)
            .Select(item => new LeaderboardMetric(
                item.Id,
                item.DisplayName,
                item.LeaderboardParticipationMode,
                item.MetricValue))
            .ToListAsync(cancellationToken);

        return BuildLeaderboardPage(
            LeaderboardType.CompletedTasks,
            pageItems,
            page,
            pageSize,
            totalCount);
    }

    private static LeaderboardPage BuildLeaderboardPage(
        LeaderboardType type,
        IReadOnlyList<LeaderboardMetric> pageItems,
        int page,
        int pageSize,
        int totalCount)
    {
        var offset = (page - 1) * pageSize;

        var entries = pageItems
            .Select((item, index) =>
            {
                var identity = ResolveIdentity(item);

                return new LeaderboardEntry(
                    offset + index + 1,
                    identity.PublicIdentity,
                    identity.IdentityMode,
                    ResolveAvatarMarker(item.UserId),
                    item.MetricValue,
                    identity.IdentityMode == LeaderboardIdentityMode.Public
                        ? ResolvePublicProfileHandle(item.UserId)
                        : null);
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

    private static string ResolvePublicProfileHandle(Guid userId)
    {
        return $"p-{userId:N}";
    }

    private static bool TryParsePublicProfileHandle(string profileHandle, out Guid userId)
    {
        userId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(profileHandle))
        {
            return false;
        }

        const string prefix = "p-";
        if (!profileHandle.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParseExact(profileHandle[prefix.Length..], "N", out userId);
    }

    private static string? NormalizeAnomalyType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim() switch
        {
            "activitySpike" => "activitySpike",
            "rankingMismatch" => "rankingMismatch",
            _ => null
        };
    }

    private static bool TryParseCaseIdentity(string caseId, out string anomalyType, out Guid userId)
    {
        anomalyType = string.Empty;
        userId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(caseId))
        {
            return false;
        }

        const string rankingPrefix = "ranking-mismatch-";
        const string spikePrefix = "activity-spike-";

        if (caseId.StartsWith(rankingPrefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(caseId[rankingPrefix.Length..], "N", out userId))
        {
            anomalyType = "rankingMismatch";
            return true;
        }

        if (caseId.StartsWith(spikePrefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParseExact(caseId[spikePrefix.Length..], "N", out userId))
        {
            anomalyType = "activitySpike";
            return true;
        }

        return false;
    }

    private static IReadOnlyCollection<SuspiciousActivityCase> BuildSuspiciousCases(
        UserAnomalySignal signal,
        string? anomalyType,
        DateTime now)
    {
        var identity = ResolveIdentity(new LeaderboardMetric(
            signal.Id,
            signal.DisplayName,
            signal.LeaderboardParticipationMode,
            signal.CompletedCount));

        var items = new List<SuspiciousActivityCase>();

        if ((anomalyType is null || anomalyType == "activitySpike")
            && signal.RecentCompletedCount >= ActivitySpikeThreshold)
        {
            var severity = Math.Min(100, 45 + (signal.RecentCompletedCount * 6));
            items.Add(new SuspiciousActivityCase(
                CaseId: $"activity-spike-{signal.Id:N}",
                PublicIdentity: identity.PublicIdentity,
                IdentityMode: identity.IdentityMode,
                AnomalyType: "activitySpike",
                SignalSummary: $"{signal.RecentCompletedCount} completions in the last 7 days.",
                Severity: severity,
                DetectedAtUtc: signal.LatestCompletedAtUtc ?? now,
                LastActivityAtUtc: signal.LatestCompletedAtUtc,
                CorrelationRef: $"corr-activity-{signal.Id:N}"));
        }

        if ((anomalyType is null || anomalyType == "rankingMismatch")
            && signal.CompletedCount >= RankingMismatchCompletedThreshold
            && signal.CurrentStreakDays <= 1)
        {
            var severity = Math.Min(100, 40 + (signal.CompletedCount * 3));
            items.Add(new SuspiciousActivityCase(
                CaseId: $"ranking-mismatch-{signal.Id:N}",
                PublicIdentity: identity.PublicIdentity,
                IdentityMode: identity.IdentityMode,
                AnomalyType: "rankingMismatch",
                SignalSummary: $"{signal.CompletedCount} total completions with a {signal.CurrentStreakDays}-day current streak.",
                Severity: severity,
                DetectedAtUtc: signal.LatestCompletedAtUtc ?? now,
                LastActivityAtUtc: signal.LatestCompletedAtUtc,
                CorrelationRef: $"corr-ranking-{signal.Id:N}"));
        }

        return items;
    }

    private sealed record ResolvedIdentity(string PublicIdentity, LeaderboardIdentityMode IdentityMode);

    private sealed record UserAnomalySignal(
        Guid Id,
        string DisplayName,
        LeaderboardParticipationMode LeaderboardParticipationMode,
        int CompletedCount,
        int RecentCompletedCount,
        int CurrentStreakDays,
        DateTime? LatestCompletedAtUtc);

    private sealed record LeaderboardMetric(
        Guid UserId,
        string DisplayName,
        LeaderboardParticipationMode ParticipationMode,
        int MetricValue);
}