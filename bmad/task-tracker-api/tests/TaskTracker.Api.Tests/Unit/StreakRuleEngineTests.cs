using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Features.Tasks.Streaks;

namespace TaskTracker.Api.Tests.Unit;

public class StreakRuleEngineTests
{
    private readonly IStreakRuleEngine _engine = new StreakRuleEngine();

    [Fact]
    public void Evaluate_FirstCompletion_ReturnsRestart()
    {
        var result = _engine.Evaluate(
            "UTC",
            new DateTime(2026, 4, 28, 9, 0, 0, DateTimeKind.Utc),
            resultingIsCompleted: true,
            completionOccurredAtUtc: [new DateTime(2026, 4, 28, 9, 0, 0, DateTimeKind.Utc)]);

        Assert.Equal(TaskStreakOutcome.Restart, result.Outcome);
        Assert.Equal(1, result.CurrentStreakDays);
        Assert.Equal(1, result.LongestStreakDays);
    }

    [Fact]
    public void Evaluate_ConsecutiveLocalDay_ReturnsContinue()
    {
        var result = _engine.Evaluate(
            "UTC",
            new DateTime(2026, 4, 28, 9, 0, 0, DateTimeKind.Utc),
            resultingIsCompleted: true,
            completionOccurredAtUtc:
            [
                new DateTime(2026, 4, 27, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 28, 9, 0, 0, DateTimeKind.Utc)
            ]);

        Assert.Equal(TaskStreakOutcome.Continue, result.Outcome);
        Assert.Equal(2, result.CurrentStreakDays);
        Assert.Equal(2, result.LongestStreakDays);
    }

    [Fact]
    public void Evaluate_GapInActivity_ReturnsRestart()
    {
        var result = _engine.Evaluate(
            "UTC",
            new DateTime(2026, 4, 28, 9, 0, 0, DateTimeKind.Utc),
            resultingIsCompleted: true,
            completionOccurredAtUtc:
            [
                new DateTime(2026, 4, 24, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 4, 28, 9, 0, 0, DateTimeKind.Utc)
            ]);

        Assert.Equal(TaskStreakOutcome.Restart, result.Outcome);
        Assert.Equal(1, result.CurrentStreakDays);
        Assert.Equal(1, result.LongestStreakDays);
    }

    [Fact]
    public void Evaluate_NotCompleted_ReturnsReset()
    {
        var result = _engine.Evaluate(
            "UTC",
            new DateTime(2026, 4, 28, 9, 0, 0, DateTimeKind.Utc),
            resultingIsCompleted: false,
            completionOccurredAtUtc:
            [
                new DateTime(2026, 4, 27, 8, 0, 0, DateTimeKind.Utc)
            ]);

        Assert.Equal(TaskStreakOutcome.Reset, result.Outcome);
        Assert.Equal(0, result.CurrentStreakDays);
        Assert.Equal(1, result.LongestStreakDays);
    }

    [Fact]
    public void Evaluate_UsesLocalDayProjectionAroundMidnight()
    {
        var result = _engine.Evaluate(
            "America/New_York",
            new DateTime(2026, 5, 2, 4, 30, 0, DateTimeKind.Utc),
            resultingIsCompleted: true,
            completionOccurredAtUtc:
            [
                new DateTime(2026, 5, 1, 4, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 2, 4, 30, 0, DateTimeKind.Utc)
            ]);

        Assert.Equal(TaskStreakOutcome.Continue, result.Outcome);
        Assert.Equal(2, result.CurrentStreakDays);
    }

    [Fact]
    public void Evaluate_HandlesDstTransitionDeterministically()
    {
        var result = _engine.Evaluate(
            "America/Los_Angeles",
            new DateTime(2026, 3, 9, 7, 30, 0, DateTimeKind.Utc),
            resultingIsCompleted: true,
            completionOccurredAtUtc:
            [
                new DateTime(2026, 3, 8, 8, 30, 0, DateTimeKind.Utc),
                new DateTime(2026, 3, 9, 7, 30, 0, DateTimeKind.Utc)
            ],
            availableRecoveryTokens: 0);

        Assert.Equal(TaskStreakOutcome.Continue, result.Outcome);
        Assert.Equal(2, result.CurrentStreakDays);
        Assert.True(result.EvaluationWindowEndUtc > result.EvaluationWindowStartUtc);
    }

    [Fact]
    public void Evaluate_MissedSingleDay_WithRecoveryToken_ConsumesTokenAndPreservesContinuity()
    {
        var result = _engine.Evaluate(
            "UTC",
            new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
            resultingIsCompleted: true,
            completionOccurredAtUtc:
            [
                new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc)
            ],
            availableRecoveryTokens: 1);

        Assert.Equal(TaskStreakOutcome.Continue, result.Outcome);
        Assert.True(result.RecoveryTokenConsumed);
        Assert.Equal(0, result.RemainingRecoveryTokens);
        Assert.Equal(3, result.CurrentStreakDays);
    }

    [Fact]
    public void Evaluate_MissedSingleDay_WithoutRecoveryToken_Restarts()
    {
        var result = _engine.Evaluate(
            "UTC",
            new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc),
            resultingIsCompleted: true,
            completionOccurredAtUtc:
            [
                new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 5, 3, 10, 0, 0, DateTimeKind.Utc)
            ],
            availableRecoveryTokens: 0);

        Assert.Equal(TaskStreakOutcome.Restart, result.Outcome);
        Assert.False(result.RecoveryTokenConsumed);
        Assert.Equal(0, result.RemainingRecoveryTokens);
        Assert.Equal(1, result.CurrentStreakDays);
    }
}
