using TaskTracker.Api.Features.Tasks.Contracts;

namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class UserStreakSnapshot
{
    public Guid OwnerId { get; set; }

    public TaskStreakOutcome Outcome { get; set; }

    public int CurrentStreakDays { get; set; }

    public int LongestStreakDays { get; set; }

    public string TimeZoneId { get; set; } = "UTC";

    public DateTime EvaluationWindowStartUtc { get; set; }

    public DateTime EvaluationWindowEndUtc { get; set; }

    public int RecoveryTokenBalance { get; set; }

    public string RecoveryTokenWeekKey { get; set; } = string.Empty;

    public DateTime? LastRecoveryTokenGrantedAtUtc { get; set; }

    public DateTime? LastRecoveryTokenConsumedAtUtc { get; set; }

    public Guid LastEvaluatedEventId { get; set; }

    public string LastEvaluationTraceId { get; set; } = string.Empty;

    public DateTime LastEvaluatedAtUtc { get; set; }
}
