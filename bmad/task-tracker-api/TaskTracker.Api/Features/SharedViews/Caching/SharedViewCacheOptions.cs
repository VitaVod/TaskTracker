namespace TaskTracker.Api.Features.SharedViews.Caching;

public class SharedViewCacheOptions
{
    public const string SectionName = "SharedViewCache";

    public string KeyPrefix { get; set; } = "tasktracker:shared-views";

    public int LeaderboardTtlSeconds { get; set; } = 120;

    public int GlobalStatisticsTtlSeconds { get; set; } = 120;

    public int FreshnessWindowSeconds { get; set; } = 30;

    public int DuplicateInvalidationSuppressionSeconds { get; set; } = 30;

    public int GenerationTtlHours { get; set; } = 24;
}
