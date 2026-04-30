using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using TaskTracker.Api.Features.Leaderboards.Contracts;
using TaskTracker.Api.Features.Leaderboards.Repositories;

namespace TaskTracker.Api.Features.SharedViews.Caching;

public class SharedViewCacheCoordinator(
    IDistributedCache cache,
    IOptions<SharedViewCacheOptions> options,
    ILogger<SharedViewCacheCoordinator> logger,
    IHttpContextAccessor httpContextAccessor) : ISharedViewCacheCoordinator
{
    private const int LeaderboardSchemaVersion = 1;
    private const int GlobalStatisticsSchemaVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task<LeaderboardPage> GetOrCreateLeaderboardAsync(
        LeaderboardType type,
        int page,
        int pageSize,
        Func<CancellationToken, Task<LeaderboardPage>> factory,
        CancellationToken cancellationToken)
    {
        return GetOrCreateLeaderboardPayloadAsync(type, page, pageSize, factory, cancellationToken);
    }

    public async Task<(long TotalTasksCreated, long TotalTasksCompleted)> GetOrCreateGlobalStatisticsAsync(
        Func<CancellationToken, Task<(long TotalTasksCreated, long TotalTasksCompleted)>> factory,
        CancellationToken cancellationToken)
    {
        var payload = await GetOrCreateScopedAsync(
            scope: "statistics:global",
            generationScope: "statistics",
            schemaVersion: GlobalStatisticsSchemaVersion,
            ttl: TimeSpan.FromSeconds(GetSanitizedOptions().GlobalStatisticsTtlSeconds),
            freshnessWindow: TimeSpan.FromSeconds(GetSanitizedOptions().FreshnessWindowSeconds),
            valueFactory: async ct =>
            {
                var (created, completed) = await factory(ct);
                return new GlobalStatisticsCachePayload(created, completed);
            },
            cancellationToken);

        return (payload.TotalTasksCreated, payload.TotalTasksCompleted);
    }

    public async Task InvalidateAfterCompletionCommitAsync(
        string idempotencyKey,
        string traceId,
        CancellationToken cancellationToken)
    {
        var normalizedIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? "missing"
            : idempotencyKey.Trim();

        var opts = GetSanitizedOptions();
        var keyPrefix = NormalizeKeyPrefix(opts.KeyPrefix);
        var suppressionKey = $"{keyPrefix}:invalidate:idempotency:{normalizedIdempotencyKey}";

        var existingTraceId = await cache.GetStringAsync(suppressionKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(existingTraceId))
        {
            logger.LogWarning(
                "cache.anomaly.duplicate_invalidation_suppressed scope=shared-views idempotencyKey={IdempotencyKey} traceId={TraceId} existingTraceId={ExistingTraceId}",
                normalizedIdempotencyKey,
                traceId,
                existingTraceId);

            return;
        }

        var generationOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(opts.GenerationTtlHours)
        };

        await cache.SetStringAsync(
            GetGenerationKey(keyPrefix, "leaderboards"),
            Guid.NewGuid().ToString("N"),
            generationOptions,
            cancellationToken);

        await cache.SetStringAsync(
            GetGenerationKey(keyPrefix, "statistics"),
            Guid.NewGuid().ToString("N"),
            generationOptions,
            cancellationToken);

        await cache.SetStringAsync(
            suppressionKey,
            traceId,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(opts.DuplicateInvalidationSuppressionSeconds)
            },
            cancellationToken);

        logger.LogInformation(
            "cache.invalidate scope=shared-views idempotencyKey={IdempotencyKey} traceId={TraceId}",
            normalizedIdempotencyKey,
            traceId);
    }

    private async Task<T> GetOrCreateScopedAsync<T>(
        string scope,
        string generationScope,
        int schemaVersion,
        TimeSpan ttl,
        TimeSpan freshnessWindow,
        Func<CancellationToken, Task<T>> valueFactory,
        CancellationToken cancellationToken)
    {
        var traceId = ResolveTraceId();
        var opts = GetSanitizedOptions();
        var keyPrefix = NormalizeKeyPrefix(opts.KeyPrefix);

        var generation = await GetOrCreateGenerationAsync(keyPrefix, generationScope, opts.GenerationTtlHours, cancellationToken);
        var cacheKey = $"{keyPrefix}:v{schemaVersion}:g{generation}:{scope}";
        var cachedBytes = await cache.GetAsync(cacheKey, cancellationToken);

        if (cachedBytes is not null)
        {
            CacheEnvelope<T>? envelope = null;

            try
            {
                envelope = JsonSerializer.Deserialize<CacheEnvelope<T>>(cachedBytes, SerializerOptions);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "cache.anomaly.deserialize_failed scope={Scope} key={CacheKey} traceId={TraceId}",
                    scope,
                    cacheKey,
                    traceId);
            }

            if (envelope is not null)
            {
                var age = DateTimeOffset.UtcNow - envelope.CachedAtUtc;
                if (age <= freshnessWindow)
                {
                    logger.LogInformation(
                        "cache.hit scope={Scope} key={CacheKey} ageMs={AgeMs} traceId={TraceId}",
                        scope,
                        cacheKey,
                        age.TotalMilliseconds,
                        traceId);

                    return envelope.Payload;
                }

                logger.LogWarning(
                    "cache.anomaly.stale_detected scope={Scope} key={CacheKey} ageMs={AgeMs} freshnessWindowMs={FreshnessWindowMs} traceId={TraceId}",
                    scope,
                    cacheKey,
                    age.TotalMilliseconds,
                    freshnessWindow.TotalMilliseconds,
                    traceId);
            }
        }
        else
        {
            logger.LogInformation(
                "cache.miss scope={Scope} key={CacheKey} traceId={TraceId}",
                scope,
                cacheKey,
                traceId);
        }

        var value = await valueFactory(cancellationToken);
        var refreshedEnvelope = new CacheEnvelope<T>(DateTimeOffset.UtcNow, value);
        var refreshedBytes = JsonSerializer.SerializeToUtf8Bytes(refreshedEnvelope, SerializerOptions);

        await cache.SetAsync(
            cacheKey,
            refreshedBytes,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl
            },
            cancellationToken);

        logger.LogInformation(
            "cache.refresh scope={Scope} key={CacheKey} ttlMs={TtlMs} traceId={TraceId}",
            scope,
            cacheKey,
            ttl.TotalMilliseconds,
            traceId);

        return value;
    }

    private async Task<string> GetOrCreateGenerationAsync(
        string keyPrefix,
        string scope,
        int generationTtlHours,
        CancellationToken cancellationToken)
    {
        var generationKey = GetGenerationKey(keyPrefix, scope);
        var generation = await cache.GetStringAsync(generationKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(generation))
        {
            return generation;
        }

        generation = Guid.NewGuid().ToString("N");

        await cache.SetStringAsync(
            generationKey,
            generation,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(generationTtlHours)
            },
            cancellationToken);

        return generation;
    }

    private static string GetGenerationKey(string keyPrefix, string scope)
    {
        return $"{keyPrefix}:generation:{scope}";
    }

    private string ResolveTraceId()
    {
        return httpContextAccessor.HttpContext?.TraceIdentifier ?? "n/a";
    }

    private static string NormalizeKeyPrefix(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "tasktracker:shared-views" : value.Trim();
        return normalized.TrimEnd(':');
    }

    private SharedViewCacheOptions GetSanitizedOptions()
    {
        var source = options.Value;

        return new SharedViewCacheOptions
        {
            KeyPrefix = source.KeyPrefix,
            LeaderboardTtlSeconds = Math.Max(1, source.LeaderboardTtlSeconds),
            GlobalStatisticsTtlSeconds = Math.Max(1, source.GlobalStatisticsTtlSeconds),
            FreshnessWindowSeconds = Math.Max(1, source.FreshnessWindowSeconds),
            DuplicateInvalidationSuppressionSeconds = Math.Max(1, source.DuplicateInvalidationSuppressionSeconds),
            GenerationTtlHours = Math.Max(1, source.GenerationTtlHours)
        };
    }

    private async Task<LeaderboardPage> GetOrCreateLeaderboardPayloadAsync(
        LeaderboardType type,
        int page,
        int pageSize,
        Func<CancellationToken, Task<LeaderboardPage>> factory,
        CancellationToken cancellationToken)
    {
        var payload = await GetOrCreateScopedAsync(
            scope: $"leaderboards:{type}:page:{page}:pageSize:{pageSize}",
            generationScope: "leaderboards",
            schemaVersion: LeaderboardSchemaVersion,
            ttl: TimeSpan.FromSeconds(GetSanitizedOptions().LeaderboardTtlSeconds),
            freshnessWindow: TimeSpan.FromSeconds(GetSanitizedOptions().FreshnessWindowSeconds),
            valueFactory: async ct =>
            {
                var result = await factory(ct);
                return new LeaderboardCachePayload(
                    (int)result.Type,
                    result.Page,
                    result.PageSize,
                    result.TotalCount,
                    result.Items.ToArray());
            },
            cancellationToken);

        return new LeaderboardPage(
            (LeaderboardType)payload.Type,
            payload.Page,
            payload.PageSize,
            payload.TotalCount,
            payload.Items);
    }

    private sealed record CacheEnvelope<T>(DateTimeOffset CachedAtUtc, T Payload);

    private sealed record LeaderboardCachePayload(
        int Type,
        int Page,
        int PageSize,
        int TotalCount,
        LeaderboardEntry[] Items);

    private sealed record GlobalStatisticsCachePayload(long TotalTasksCreated, long TotalTasksCompleted);
}
