using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Features.Progress.Contracts;
using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Infrastructure.Persistence;
using TimeZoneConverter;

namespace TaskTracker.Api.Features.Progress.Repositories;

public class ProgressRepository(TaskTrackerDbContext dbContext) : IProgressRepository
{
    public async Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId, cancellationToken);
    }

    public async Task<ProgressXpSummary> GetXpSummaryAsync(Guid userId, CancellationToken cancellationToken)
    {
        var summary = await dbContext.XpLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == userId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalXp = group.Sum(entry => entry.XpGranted),
                LedgerEntryCount = group.Count(),
                LastGrantedAtUtc = group.Max(entry => (DateTime?)entry.OccurredAtUtc)
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (summary is null)
        {
            return new ProgressXpSummary(0, 0, null);
        }

        return new ProgressXpSummary(summary.TotalXp, summary.LedgerEntryCount, summary.LastGrantedAtUtc);
    }

    public async Task<ProgressStreakSnapshot> GetStreakSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.UserStreakSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(existingSnapshot => existingSnapshot.OwnerId == userId, cancellationToken);

        if (snapshot is not null)
        {
            return new ProgressStreakSnapshot(
                snapshot.Outcome,
                snapshot.CurrentStreakDays,
                snapshot.LongestStreakDays,
                snapshot.TimeZoneId,
                snapshot.EvaluationWindowStartUtc,
                snapshot.EvaluationWindowEndUtc,
                snapshot.LastEvaluatedAtUtc);
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstAsync(existingUser => existingUser.Id == userId, cancellationToken);

        var timeZoneId = ResolveTimeZoneId(user.TimeZoneId);
        var nowUtc = DateTime.UtcNow;

        return new ProgressStreakSnapshot(
            TaskStreakOutcome.Reset,
            0,
            0,
            timeZoneId,
            nowUtc,
            nowUtc,
            nowUtc);
    }

    public async Task<ProgressTrendSummary> GetTrendSummaryAsync(
        Guid userId,
        ProgressTrendGranularity granularity,
        int windowDays,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstAsync(existingUser => existingUser.Id == userId, cancellationToken);

        var timeZone = ResolveTimeZone(user.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var localEndDate = DateOnly.FromDateTime(localNow.Date);
        var localStartDate = localEndDate.AddDays(-(windowDays - 1));
        var bucketStartDate = granularity == ProgressTrendGranularity.Weekly
            ? StartOfWeek(localStartDate)
            : localStartDate;

        var rangeStartUtc = ToUtc(bucketStartDate, timeZone);
        var rangeEndUtcExclusive = ToUtc(localEndDate.AddDays(1), timeZone);

        var completedEvents = await dbContext.TaskCompletionEvents
            .AsNoTracking()
            .Where(completionEvent => completionEvent.OwnerId == userId
                && completionEvent.EventName == "TaskCompleted"
                && completionEvent.OccurredAtUtc >= rangeStartUtc
                && completionEvent.OccurredAtUtc < rangeEndUtcExclusive)
            .Select(completionEvent => completionEvent.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        var xpEntries = await dbContext.XpLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == userId
                && entry.OccurredAtUtc >= rangeStartUtc
                && entry.OccurredAtUtc < rangeEndUtcExclusive)
            .Select(entry => new { entry.OccurredAtUtc, entry.XpGranted })
            .ToListAsync(cancellationToken);

        var buckets = BuildBuckets(bucketStartDate, localEndDate, granularity, timeZone);
        var bucketLookup = buckets.ToDictionary(
            bucket => bucket.BucketStartUtc,
            bucket => new BucketAggregate(bucket.BucketStartUtc, bucket.BucketEndUtc),
            EqualityComparer<DateTime>.Default);

        foreach (var occurredAtUtc in completedEvents)
        {
            var bucketStartUtc = ResolveBucketStartUtc(occurredAtUtc, granularity, timeZone);
            if (bucketLookup.TryGetValue(bucketStartUtc, out var bucket))
            {
                bucket.CompletedTaskCount += 1;
            }
        }

        foreach (var entry in xpEntries)
        {
            var bucketStartUtc = ResolveBucketStartUtc(entry.OccurredAtUtc, granularity, timeZone);
            if (bucketLookup.TryGetValue(bucketStartUtc, out var bucket))
            {
                bucket.XpGranted += entry.XpGranted;
            }
        }

        var items = bucketLookup.Values
            .OrderBy(bucket => bucket.BucketStartUtc)
            .Select(bucket => new ProgressTrendPoint(
                bucket.BucketStartUtc,
                bucket.BucketEndUtc,
                bucket.CompletedTaskCount,
                bucket.XpGranted))
            .ToArray();

        var rangeEndUtc = items.Length == 0
            ? rangeStartUtc
            : items[^1].BucketEndUtc;

        return new ProgressTrendSummary(
            timeZone.Id,
            rangeStartUtc,
            rangeEndUtc,
            items);
    }

    private sealed record Bucket(DateTime BucketStartUtc, DateTime BucketEndUtc);

    private sealed class BucketAggregate(DateTime bucketStartUtc, DateTime bucketEndUtc)
    {
        public DateTime BucketStartUtc { get; } = bucketStartUtc;

        public DateTime BucketEndUtc { get; } = bucketEndUtc;

        public int CompletedTaskCount { get; set; }

        public int XpGranted { get; set; }
    }

    private static IReadOnlyCollection<Bucket> BuildBuckets(
        DateOnly localStartDate,
        DateOnly localEndDate,
        ProgressTrendGranularity granularity,
        TimeZoneInfo timeZone)
    {
        if (granularity == ProgressTrendGranularity.Daily)
        {
            var dailyBuckets = new List<Bucket>();
            var cursor = localStartDate;

            while (cursor <= localEndDate)
            {
                dailyBuckets.Add(new Bucket(
                    ToUtc(cursor, timeZone),
                    ToUtc(cursor.AddDays(1), timeZone)));
                cursor = cursor.AddDays(1);
            }

            return dailyBuckets;
        }

        var weeklyBuckets = new List<Bucket>();
        var weeklyCursor = StartOfWeek(localStartDate);

        while (weeklyCursor <= localEndDate)
        {
            weeklyBuckets.Add(new Bucket(
                ToUtc(weeklyCursor, timeZone),
                ToUtc(weeklyCursor.AddDays(7), timeZone)));
            weeklyCursor = weeklyCursor.AddDays(7);
        }

        return weeklyBuckets;
    }

    private static DateTime ResolveBucketStartUtc(DateTime occurredAtUtc, ProgressTrendGranularity granularity, TimeZoneInfo timeZone)
    {
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(occurredAtUtc, timeZone));
        var bucketLocalDate = granularity == ProgressTrendGranularity.Daily
            ? localDate
            : StartOfWeek(localDate);

        return ToUtc(bucketLocalDate, timeZone);
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var distanceFromMonday = (dayOfWeek + 6) % 7;
        return date.AddDays(-distanceFromMonday);
    }

    private static DateTime ToUtc(DateOnly localDate, TimeZoneInfo timeZone)
    {
        var localDateTime = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localDateTime, timeZone);
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TZConvert.GetTimeZoneInfo(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    private static string ResolveTimeZoneId(string timeZoneId)
    {
        return ResolveTimeZone(timeZoneId).Id;
    }
}
