using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskTracker.Api.Features.Progress.Configuration;
using TaskTracker.Api.Features.Progress.Contracts;
using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Infrastructure.Persistence;
using TimeZoneConverter;

namespace TaskTracker.Api.Features.Progress.Repositories;

public class ProgressRepository(
    TaskTrackerDbContext dbContext,
    IOptions<ProgressionLevelOptions> progressionLevelOptions) : IProgressRepository
{
    private const string XpReasonNoLedgerEvents = "xp-no-ledger-events";
    private const string XpReasonEarnedFromCompletions = "xp-earned-from-completions";
    private const string XpReasonLedgerRecordedNoNetGain = "xp-ledger-recorded-no-net-gain";
    private const string StreakReasonContinued = "streak-continued";
    private const string StreakReasonRestarted = "streak-restarted";
    private const string StreakReasonReset = "streak-reset";
    private const string RecoveryReasonMissedDayDetected = "missed-day-detected";
    private const string RecoveryReasonStreakRestarted = "streak-restarted";
    private const string RecoveryActionCompleteTaskToday = "complete-task-today";
    private const string RecoveryActionMaintainTomorrow = "maintain-tomorrow";
    private readonly ProgressionLevelOptions _progressionLevels = progressionLevelOptions.Value;

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
            return new ProgressXpSummary(
                0,
                0,
                null,
                BuildLevelSnapshot(0),
                XpReasonNoLedgerEvents,
                "XP has not changed yet because no eligible task completions were recorded.");
        }

        var (reasonCode, explanation) = BuildXpOutcomeExplanation(summary.TotalXp, summary.LedgerEntryCount);

        return new ProgressXpSummary(
            summary.TotalXp,
            summary.LedgerEntryCount,
            summary.LastGrantedAtUtc,
            BuildLevelSnapshot(summary.TotalXp),
            reasonCode,
            explanation);
    }

    public async Task<ProgressStreakSnapshot> GetStreakSnapshotAsync(Guid userId, CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.UserStreakSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(existingSnapshot => existingSnapshot.OwnerId == userId, cancellationToken);

        if (snapshot is not null)
        {
            var recoverySignal = BuildRecoverySignal(
                snapshot.Outcome,
                snapshot.CurrentStreakDays,
                snapshot.LastEvaluatedAtUtc,
                snapshot.TimeZoneId,
                DateTime.UtcNow);

            var (streakReasonCode, streakExplanation) = BuildStreakOutcomeExplanation(snapshot.Outcome, snapshot.CurrentStreakDays);

            return new ProgressStreakSnapshot(
                snapshot.Outcome,
                snapshot.CurrentStreakDays,
                snapshot.LongestStreakDays,
                snapshot.TimeZoneId,
                snapshot.EvaluationWindowStartUtc,
                snapshot.EvaluationWindowEndUtc,
                snapshot.LastEvaluatedAtUtc,
                recoverySignal.IsVisible,
                recoverySignal.Reason,
                recoverySignal.RecommendedAction,
                streakReasonCode,
                streakExplanation,
                BuildRecoveryExplanation(recoverySignal.Reason));
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
            nowUtc,
            false,
            null,
            null,
            StreakReasonReset,
            "Your streak is currently at zero and will begin after your next eligible completion.",
            null);
    }

    private ProgressLevelSnapshot BuildLevelSnapshot(int totalXp)
    {
        var nonNegativeXp = Math.Max(0, totalXp);
        var startingLevel = Math.Max(1, _progressionLevels.StartingLevel);
        var baseXp = Math.Max(1, _progressionLevels.BaseXpPerLevel);
        var growthXp = Math.Max(0, _progressionLevels.GrowthXpPerLevel);

        var currentLevel = startingLevel;
        var currentLevelThresholdXp = 0;
        var nextLevel = startingLevel + 1;
        var nextLevelThresholdXp = baseXp;

        while (nonNegativeXp >= nextLevelThresholdXp)
        {
            currentLevel = nextLevel;
            currentLevelThresholdXp = nextLevelThresholdXp;
            nextLevel += 1;

            var increment = baseXp + ((nextLevel - startingLevel - 1) * growthXp);
            nextLevelThresholdXp += Math.Max(1, increment);
        }

        var span = Math.Max(1, nextLevelThresholdXp - currentLevelThresholdXp);
        var progressed = Math.Clamp(nonNegativeXp - currentLevelThresholdXp, 0, span);
        var percentToNextLevel = Math.Round((progressed * 100d) / span, 2, MidpointRounding.AwayFromZero);

        var milestoneLevels = (_progressionLevels.BandMilestoneLevels ?? [])
            .Where(level => level >= startingLevel)
            .Distinct()
            .OrderBy(level => level)
            .ToArray();

        var reachedBandCount = milestoneLevels.Count(level => currentLevel >= level);
        var nextBandLevel = milestoneLevels.FirstOrDefault(level => currentLevel < level);

        return new ProgressLevelSnapshot(
            currentLevel,
            currentLevelThresholdXp,
            nextLevel,
            nextLevelThresholdXp,
            percentToNextLevel,
            milestoneLevels,
            reachedBandCount,
            nextBandLevel == 0 ? null : nextBandLevel);
    }

    private static (string ReasonCode, string Message) BuildXpOutcomeExplanation(int totalXp, int ledgerEntryCount)
    {
        if (ledgerEntryCount == 0)
        {
            return (
                XpReasonNoLedgerEvents,
                "XP has not changed yet because no eligible task completions were recorded.");
        }

        if (totalXp > 0)
        {
            return (
                XpReasonEarnedFromCompletions,
                "XP increased from eligible task completion events processed by the progression engine.");
        }

        return (
            XpReasonLedgerRecordedNoNetGain,
            "Progress events were recorded, but your net XP has not increased in this summary.");
    }

    private static (string ReasonCode, string Message) BuildStreakOutcomeExplanation(TaskStreakOutcome outcome, int currentStreakDays)
    {
        return outcome switch
        {
            TaskStreakOutcome.Continue => (
                StreakReasonContinued,
                $"Your streak is active at {currentStreakDays} day(s) because completions stayed within the allowed local-day window."),
            TaskStreakOutcome.Restart => (
                StreakReasonRestarted,
                "Your streak restarted after a missed continuity window and now counts from your latest eligible completion."),
            _ => (
                StreakReasonReset,
                "Your streak is currently at zero and will begin after your next eligible completion.")
        };
    }

    private static string? BuildRecoveryExplanation(string? recoveryReason)
    {
        return recoveryReason switch
        {
            RecoveryReasonMissedDayDetected =>
                "A missed local day was detected. Completing one eligible task today starts the next streak immediately.",
            RecoveryReasonStreakRestarted =>
                "Your streak has restarted. Completing at least one eligible task in the next local-day window keeps it active.",
            _ => null
        };
    }

    private static (bool IsVisible, string? Reason, string? RecommendedAction) BuildRecoverySignal(
        TaskStreakOutcome outcome,
        int currentStreakDays,
        DateTime lastEvaluatedAtUtc,
        string timeZoneId,
        DateTime nowUtc)
    {
        var timeZone = ResolveTimeZone(timeZoneId);
        var localNowDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(nowUtc), timeZone));
        var localLastEvaluatedDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(lastEvaluatedAtUtc), timeZone));
        var localDayGap = localNowDate.DayNumber - localLastEvaluatedDate.DayNumber;

        if (currentStreakDays > 0 && localDayGap > 1)
        {
            return (true, RecoveryReasonMissedDayDetected, RecoveryActionCompleteTaskToday);
        }

        if (outcome == TaskStreakOutcome.Restart)
        {
            return (true, RecoveryReasonStreakRestarted, RecoveryActionMaintainTomorrow);
        }

        return (false, null, null);
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

        var completionStateEvents = await dbContext.TaskCompletionEvents
            .AsNoTracking()
            .Where(completionEvent => completionEvent.OwnerId == userId)
            .Select(completionEvent => new CompletionStateEvent(
                completionEvent.TaskId,
                completionEvent.EventName,
                completionEvent.ResultingIsCompleted,
                completionEvent.OccurredAtUtc,
                completionEvent.CreatedAtUtc,
                completionEvent.Id))
            .ToListAsync(cancellationToken);

        var completedEvents = BuildEffectiveCompletionOccurredAtUtc(completionStateEvents)
            .Where(occurredAtUtc => occurredAtUtc >= rangeStartUtc && occurredAtUtc < rangeEndUtcExclusive)
            .ToArray();

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

    private sealed record CompletionStateEvent(
        Guid TaskId,
        string EventName,
        bool ResultingIsCompleted,
        DateTime OccurredAtUtc,
        DateTime CreatedAtUtc,
        Guid EventId);

    private sealed class BucketAggregate(DateTime bucketStartUtc, DateTime bucketEndUtc)
    {
        public DateTime BucketStartUtc { get; } = bucketStartUtc;

        public DateTime BucketEndUtc { get; } = bucketEndUtc;

        public int CompletedTaskCount { get; set; }

        public int XpGranted { get; set; }
    }

    private static IReadOnlyCollection<DateTime> BuildEffectiveCompletionOccurredAtUtc(
        IReadOnlyCollection<CompletionStateEvent> completionEvents)
    {
        var effectiveCompletionOccurredAtUtc = new List<DateTime>();

        foreach (var taskEvents in completionEvents
                     .GroupBy(completionEvent => completionEvent.TaskId)
                     .Select(group => group
                         .OrderBy(completionEvent => completionEvent.OccurredAtUtc)
                         .ThenBy(completionEvent => completionEvent.CreatedAtUtc)
                         .ThenBy(completionEvent => completionEvent.EventId)))
        {
            var taskEffectiveCompletions = new List<DateTime>();

            foreach (var completionEvent in taskEvents)
            {
                if (string.Equals(completionEvent.EventName, "TaskCompleted", StringComparison.Ordinal))
                {
                    taskEffectiveCompletions.Add(completionEvent.OccurredAtUtc);
                    continue;
                }

                var reopensTask = string.Equals(completionEvent.EventName, "TaskReopened", StringComparison.Ordinal)
                    || (string.Equals(completionEvent.EventName, "TaskCompletionSet", StringComparison.Ordinal)
                        && !completionEvent.ResultingIsCompleted);

                if (reopensTask && taskEffectiveCompletions.Count > 0)
                {
                    taskEffectiveCompletions.RemoveAt(taskEffectiveCompletions.Count - 1);
                }
            }

            effectiveCompletionOccurredAtUtc.AddRange(taskEffectiveCompletions);
        }

        return effectiveCompletionOccurredAtUtc;
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

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
