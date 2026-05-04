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
    IdempotentNotFound,
    CompletedTaskDeletionBlocked
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

public enum IntegrationTaskSyncStatus
{
    Created,
    Updated,
    IdempotentReplay,
    Forbidden
}

public sealed record IntegrationTaskSyncResult(
    IntegrationTaskSyncStatus Status,
    TaskItem? Task,
    string? ReplayOperation,
    string? ExternalTaskId);

public interface ITaskRepository
{
    Task CreateAsync(TaskItem task, CancellationToken cancellationToken);

    Task<IReadOnlyList<TaskItem>> ListOwnedByStateAsync(
        Guid userId,
        TaskListState state,
        string? title,
        string? priority,
        string? energyLevel,
        string? contextTag,
        CancellationToken cancellationToken);

    Task<(int ActiveCount, int CompletedCount)> CountOwnedByCompletionStateAsync(Guid userId, CancellationToken cancellationToken);

    Task<TaskUpdateResult> UpdateOwnedAsync(
        Guid userId,
        Guid taskId,
        string title,
        string description,
        DateTime? dueAtUtc,
        string priority,
        string category,
        TaskDifficulty difficulty,
        TaskEnergyLevel energyLevel,
        string? contextTag,
        int? effortPoints,
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

    Task<IntegrationTaskSyncResult> UpsertOwnedFromIntegrationAsync(
        Guid ownerUserId,
        string integrationId,
        string idempotencyKey,
        string externalTaskId,
        string title,
        string description,
        DateTime? dueAtUtc,
        string priority,
        string category,
        TaskDifficulty difficulty,
        TaskEnergyLevel energyLevel,
        string? contextTag,
        int? effortPoints,
        bool isCompleted,
        string correlationId,
        string traceId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken);
}