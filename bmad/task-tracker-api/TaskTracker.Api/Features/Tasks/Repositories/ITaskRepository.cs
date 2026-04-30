using TaskTracker.Api.Infrastructure.Persistence.Entities;
using TaskTracker.Api.Features.Tasks.Contracts;

namespace TaskTracker.Api.Features.Tasks.Repositories;

public enum TaskUpdateStatus
{
    Updated,
    Forbidden,
    NotFound
}

public enum TaskCompletionToggleStatus
{
    Updated,
    Forbidden,
    NotFound,
    IdempotentReplay,
    InvalidTimeZone
}

public enum TaskDeleteStatus
{
    Deleted,
    Forbidden,
    IdempotentNotFound
}

public sealed record TaskUpdateResult(TaskUpdateStatus Status, TaskItem? Task);

public sealed record TaskCompletionToggleResult(
    TaskCompletionToggleStatus Status,
    TaskItem? Task,
    TaskProgressionOutcome? ProgressionOutcome);

public sealed record TaskProgressionOutcome(
    Guid CompletionEventId,
    Guid? XpLedgerEntryId,
    int XpGranted,
    bool EligibleForXp,
    bool IdempotentReplay,
    string IdempotencyKey,
    TaskStreakOutcome StreakOutcome,
    int CurrentStreakDays,
    int LongestStreakDays,
    string TimeZoneId,
    DateTime EvaluationWindowStartUtc,
    DateTime EvaluationWindowEndUtc);

public sealed record TaskDeleteResult(TaskDeleteStatus Status);

public interface ITaskRepository
{
    Task CreateAsync(TaskItem task, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListOwnedByStateAsync(Guid userId, TaskListState state, CancellationToken cancellationToken);

    Task<(int ActiveCount, int CompletedCount)> CountOwnedByCompletionStateAsync(Guid userId, CancellationToken cancellationToken);

    Task<TaskUpdateResult> UpdateOwnedAsync(
        Guid userId,
        Guid taskId,
        string title,
        string description,
        DateTime? dueAtUtc,
        string priority,
        string category,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);

    Task<TaskCompletionToggleResult> ToggleCompletionOwnedAsync(
        Guid userId,
        Guid taskId,
        bool isCompleted,
        string idempotencyKey,
        string traceId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);

    Task<TaskDeleteResult> DeleteOwnedAsync(
        Guid userId,
        Guid taskId,
        CancellationToken cancellationToken);
}