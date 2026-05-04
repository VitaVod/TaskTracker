using TaskTracker.Api.Features.Tasks.Contracts;
using TimeZoneConverter;

namespace TaskTracker.Api.Features.Tasks.Streaks;

public sealed record StreakEvaluationResult(
    TaskStreakOutcome Outcome,
    int CurrentStreakDays,
    int LongestStreakDays,
    DateTime EvaluationWindowStartUtc,
    DateTime EvaluationWindowEndUtc,
    bool RecoveryTokenConsumed,
    int RemainingRecoveryTokens);

public interface IStreakRuleEngine
{
    StreakEvaluationResult Evaluate(
        string timeZoneId,
        DateTime evaluationOccurredAtUtc,
        bool resultingIsCompleted,
        IReadOnlyCollection<DateTime> completionOccurredAtUtc,
        int availableRecoveryTokens = 0);
}

public class StreakRuleEngine : IStreakRuleEngine
{
    public StreakEvaluationResult Evaluate(
        string timeZoneId,
        DateTime evaluationOccurredAtUtc,
        bool resultingIsCompleted,
        IReadOnlyCollection<DateTime> completionOccurredAtUtc,
        int availableRecoveryTokens = 0)
    {
        var timeZone = TZConvert.GetTimeZoneInfo(timeZoneId);
        var evaluationUtc = EnsureUtc(evaluationOccurredAtUtc);
        var evaluationLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(evaluationUtc, timeZone));

        var completionDates = completionOccurredAtUtc
            .Select(EnsureUtc)
            .Select(occurredAtUtc => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(occurredAtUtc, timeZone)))
            .Distinct()
            .OrderBy(localDate => localDate)
            .ToArray();

        var longestStreakDays = ComputeLongestStreak(completionDates);
        var currentStreakDays = 0;
    var recoveryTokenConsumed = false;
    var remainingRecoveryTokens = Math.Max(0, availableRecoveryTokens);
        TaskStreakOutcome outcome;

        if (!resultingIsCompleted)
        {
            outcome = TaskStreakOutcome.Reset;
        }
        else
        {
            var lastCompletionBeforeEvaluation = completionDates
                .Where(localDate => localDate < evaluationLocalDate)
                .DefaultIfEmpty()
                .Max();

            if (lastCompletionBeforeEvaluation == default)
            {
                outcome = TaskStreakOutcome.Restart;
            }
            else
            {
                var dayGap = evaluationLocalDate.DayNumber - lastCompletionBeforeEvaluation.DayNumber;
                if (dayGap <= 1)
                {
                    outcome = TaskStreakOutcome.Continue;
                }
                else if (dayGap == 2 && remainingRecoveryTokens > 0)
                {
                    outcome = TaskStreakOutcome.Continue;
                    recoveryTokenConsumed = true;
                    remainingRecoveryTokens--;
                }
                else
                {
                    outcome = TaskStreakOutcome.Restart;
                }
            }

            currentStreakDays = ComputeCurrentStreak(
                completionDates,
                evaluationLocalDate,
                allowSingleBridge: recoveryTokenConsumed);
            longestStreakDays = Math.Max(longestStreakDays, currentStreakDays);
        }

        var evaluationWindowStartUtc = ResolveLocalBoundaryToUtc(evaluationLocalDate, timeZone);
        var evaluationWindowEndUtc = ResolveLocalBoundaryToUtc(evaluationLocalDate.AddDays(1), timeZone);

        return new StreakEvaluationResult(
            outcome,
            currentStreakDays,
            longestStreakDays,
            evaluationWindowStartUtc,
            evaluationWindowEndUtc,
            recoveryTokenConsumed,
            remainingRecoveryTokens);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private static int ComputeLongestStreak(IReadOnlyList<DateOnly> completionDates)
    {
        if (completionDates.Count == 0)
        {
            return 0;
        }

        var longest = 1;
        var current = 1;

        for (var index = 1; index < completionDates.Count; index++)
        {
            var dayGap = completionDates[index].DayNumber - completionDates[index - 1].DayNumber;
            if (dayGap == 1)
            {
                current++;
            }
            else
            {
                current = 1;
            }

            if (current > longest)
            {
                longest = current;
            }
        }

        return longest;
    }

    private static int ComputeCurrentStreak(
        IReadOnlyList<DateOnly> completionDates,
        DateOnly evaluationLocalDate,
        bool allowSingleBridge)
    {
        if (completionDates.Count == 0)
        {
            return 0;
        }

        var completionDateSet = completionDates.ToHashSet();
        if (!completionDateSet.Contains(evaluationLocalDate))
        {
            return 0;
        }

        var streakLength = 0;
        var cursor = evaluationLocalDate;
        var bridgeAvailable = allowSingleBridge;

        while (true)
        {
            if (completionDateSet.Contains(cursor))
            {
                streakLength++;
                cursor = cursor.AddDays(-1);
                continue;
            }

            if (bridgeAvailable && completionDateSet.Contains(cursor.AddDays(-1)))
            {
                streakLength++;
                bridgeAvailable = false;
                cursor = cursor.AddDays(-1);
                continue;
            }

            break;
        }

        return streakLength;
    }

    private static DateTime ResolveLocalBoundaryToUtc(DateOnly localDate, TimeZoneInfo timeZone)
    {
        var localBoundary = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localBoundary, timeZone);
    }
}
