namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class StreakRecoveryTokenEvent
{
    public Guid Id { get; set; }

    public Guid OwnerId { get; set; }

    public StreakRecoveryTokenEventType EventType { get; set; }

    public string TimeZoneId { get; set; } = "UTC";

    public string LocalDate { get; set; } = string.Empty;

    public string WeekKey { get; set; } = string.Empty;

    public int BalanceAfter { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public Guid? CompletionEventId { get; set; }

    public string TraceId { get; set; } = string.Empty;
}
