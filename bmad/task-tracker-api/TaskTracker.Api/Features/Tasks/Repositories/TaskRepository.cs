using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using TaskTracker.Api.Features.SharedViews.Caching;
using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Features.Tasks.Streaks;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;
using TimeZoneConverter;

namespace TaskTracker.Api.Features.Tasks.Repositories;

public class TaskRepository(
    TaskTrackerDbContext dbContext,
    IStreakRuleEngine streakRuleEngine,
    ISharedViewCacheCoordinator sharedViewCache,
    ILogger<TaskRepository> logger) : ITaskRepository
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CompletionLocks = new();

    private const int CompletionXpAmount = 10;

    public async Task CreateAsync(TaskItem task, CancellationToken cancellationToken)
    {
        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> ListOwnedByStateAsync(
        Guid userId,
        TaskListState state,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.UserId == userId);

        query = state switch
        {
            TaskListState.Active => query.Where(task => !task.IsCompleted),
            TaskListState.Completed => query.Where(task => task.IsCompleted),
            _ => query
        };

        return await query
            .OrderBy(task => task.IsCompleted)
            .ThenByDescending(task => task.UpdatedAtUtc)
            .ThenByDescending(task => task.CreatedAtUtc)
            .ThenBy(task => task.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<(int ActiveCount, int CompletedCount)> CountOwnedByCompletionStateAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var groupedCounts = await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.UserId == userId)
            .GroupBy(task => task.IsCompleted)
            .Select(group => new
            {
                IsCompleted = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);

        var activeCount = groupedCounts.FirstOrDefault(item => !item.IsCompleted)?.Count ?? 0;
        var completedCount = groupedCounts.FirstOrDefault(item => item.IsCompleted)?.Count ?? 0;

        return (activeCount, completedCount);
    }

    public async Task<TaskUpdateResult> UpdateOwnedAsync(
        Guid userId,
        Guid taskId,
        string title,
        string description,
        DateTime? dueAtUtc,
        string priority,
        string category,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(existingTask => existingTask.Id == taskId, cancellationToken);
        if (task is null)
        {
            return new TaskUpdateResult(TaskUpdateStatus.NotFound, null);
        }

        if (task.UserId != userId)
        {
            return new TaskUpdateResult(TaskUpdateStatus.Forbidden, null);
        }

        task.Title = title;
        task.Description = description;
        task.DueAtUtc = dueAtUtc;
        task.Priority = priority;
        task.Category = category;
        task.UpdatedAtUtc = updatedAtUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TaskUpdateResult(TaskUpdateStatus.Updated, task);
    }

    public async Task<TaskCompletionToggleResult> ToggleCompletionOwnedAsync(
        Guid userId,
        Guid taskId,
        bool isCompleted,
        string idempotencyKey,
        string traceId,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(existingUser => existingUser.Id == userId, cancellationToken);

        if (user is null)
        {
            return new TaskCompletionToggleResult(TaskCompletionToggleStatus.NotFound, null, null);
        }

        if (!TryResolveTimeZone(user.TimeZoneId, out var resolvedTimeZoneId))
        {
            return new TaskCompletionToggleResult(TaskCompletionToggleStatus.InvalidTimeZone, null, null);
        }

        var lockKey = $"{userId:N}:{taskId:N}";
        var completionLock = CompletionLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await completionLock.WaitAsync(cancellationToken);

        try
        {
            var task = await dbContext.Tasks.FirstOrDefaultAsync(existingTask => existingTask.Id == taskId, cancellationToken);
            if (task is null)
            {
                return new TaskCompletionToggleResult(TaskCompletionToggleStatus.NotFound, null, null);
            }

            if (task.UserId != userId)
            {
                return new TaskCompletionToggleResult(TaskCompletionToggleStatus.Forbidden, null, null);
            }

            var existingCommand = await dbContext.TaskCompletionEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    completionEvent => completionEvent.TaskId == taskId
                        && completionEvent.OwnerId == userId
                        && completionEvent.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (existingCommand is not null)
            {
                var replayTask = await dbContext.Tasks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(existingTask => existingTask.Id == taskId && existingTask.UserId == userId, cancellationToken);

                if (replayTask is null)
                {
                    return new TaskCompletionToggleResult(TaskCompletionToggleStatus.NotFound, null, null);
                }

                var replayLedgerEntry = await dbContext.XpLedgerEntries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(entry => entry.TaskCompletionEventId == existingCommand.Id, cancellationToken);

                return new TaskCompletionToggleResult(
                    TaskCompletionToggleStatus.IdempotentReplay,
                    replayTask,
                    await BuildReplayProgressionOutcomeAsync(
                        existingCommand,
                        replayLedgerEntry,
                        idempotencyKey,
                        resolvedTimeZoneId,
                        cancellationToken));
            }

            var stateChanged = task.IsCompleted != isCompleted;
            if (stateChanged)
            {
                task.IsCompleted = isCompleted;
                task.UpdatedAtUtc = updatedAtUtc;
            }

            var completionEvent = new TaskCompletionEvent
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                OwnerId = task.UserId,
                EventName = isCompleted && stateChanged ? "TaskCompleted" : "TaskCompletionSet",
                ResultingIsCompleted = isCompleted,
                IdempotencyKey = idempotencyKey,
                OccurredAtUtc = updatedAtUtc,
                CreatedAtUtc = updatedAtUtc
            };

            dbContext.TaskCompletionEvents.Add(completionEvent);

            // Progression should only react to true completion transitions, never duplicate retries.
            var isEligibleForXp = isCompleted && stateChanged;
            XpLedgerEntry? xpLedgerEntry = null;
            if (isEligibleForXp)
            {
                xpLedgerEntry = new XpLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    OwnerId = task.UserId,
                    TaskId = task.Id,
                    TaskCompletionEventId = completionEvent.Id,
                    EventName = completionEvent.EventName,
                    IdempotencyKey = idempotencyKey,
                    XpGranted = CompletionXpAmount,
                    OccurredAtUtc = updatedAtUtc,
                    CreatedAtUtc = updatedAtUtc
                };

                dbContext.XpLedgerEntries.Add(xpLedgerEntry);
            }

            var streakOutcome = await EvaluateStreakAsync(
                userId,
                completionEvent.OccurredAtUtc,
                completionEvent.ResultingIsCompleted,
                completionEvent.EventName,
                resolvedTimeZoneId,
                cancellationToken);

            await UpsertStreakSnapshotAsync(
                userId,
                completionEvent.Id,
                traceId,
                updatedAtUtc,
                resolvedTimeZoneId,
                streakOutcome,
                cancellationToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Concurrent duplicate request with the same idempotency key.
                var replayTask = await dbContext.Tasks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(existingTask => existingTask.Id == taskId && existingTask.UserId == userId, cancellationToken);

                if (replayTask is null)
                {
                    return new TaskCompletionToggleResult(TaskCompletionToggleStatus.NotFound, null, null);
                }

                var replayCompletionEvent = await dbContext.TaskCompletionEvents
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        completion => completion.TaskId == taskId
                            && completion.OwnerId == userId
                            && completion.IdempotencyKey == idempotencyKey,
                        cancellationToken);

                TaskProgressionOutcome? replayProgression = null;
                if (replayCompletionEvent is not null)
                {
                    var replayLedgerEntry = await dbContext.XpLedgerEntries
                        .AsNoTracking()
                        .FirstOrDefaultAsync(entry => entry.TaskCompletionEventId == replayCompletionEvent.Id, cancellationToken);

                    replayProgression = await BuildReplayProgressionOutcomeAsync(
                        replayCompletionEvent,
                        replayLedgerEntry,
                        idempotencyKey,
                        resolvedTimeZoneId,
                        cancellationToken);
                }

                return new TaskCompletionToggleResult(TaskCompletionToggleStatus.IdempotentReplay, replayTask, replayProgression);
            }

            var persistedTask = await dbContext.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(existingTask => existingTask.Id == taskId && existingTask.UserId == userId, cancellationToken);

            if (persistedTask is null)
            {
                return new TaskCompletionToggleResult(TaskCompletionToggleStatus.NotFound, null, null);
            }

            if (stateChanged)
            {
                try
                {
                    await sharedViewCache.InvalidateAfterCompletionCommitAsync(idempotencyKey, traceId, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(
                        ex,
                        "Shared view cache invalidation failed after completion commit. TaskId: {TaskId}. UserId: {UserId}. TraceId: {TraceId}",
                        taskId,
                        userId,
                        traceId);
                }
            }

            return new TaskCompletionToggleResult(
                TaskCompletionToggleStatus.Updated,
                persistedTask,
                BuildProgressionOutcome(
                    completionEvent,
                    xpLedgerEntry,
                    idempotencyKey,
                    streakOutcome,
                    resolvedTimeZoneId,
                    idempotentReplay: false));
        }
        finally
        {
            completionLock.Release();

            // Keep lock registry bounded; if no waiter is queued, this lock can be discarded.
            if (completionLock.CurrentCount == 1)
            {
                CompletionLocks.TryRemove(lockKey, out _);
            }
        }
    }

    public async Task<TaskDeleteResult> DeleteOwnedAsync(
        Guid userId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks.FirstOrDefaultAsync(existingTask => existingTask.Id == taskId, cancellationToken);
        if (task is null)
        {
            return new TaskDeleteResult(TaskDeleteStatus.IdempotentNotFound);
        }

        if (task.UserId != userId)
        {
            return new TaskDeleteResult(TaskDeleteStatus.Forbidden);
        }

        dbContext.Tasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TaskDeleteResult(TaskDeleteStatus.Deleted);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        if (exception.InnerException is SqlException sqlException)
        {
            return sqlException.Number is 2601 or 2627;
        }

        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private TaskProgressionOutcome BuildProgressionOutcome(
        TaskCompletionEvent completionEvent,
        XpLedgerEntry? xpLedgerEntry,
        string idempotencyKey,
        StreakEvaluationResult streakEvaluation,
        string timeZoneId,
        bool idempotentReplay)
    {
        return new TaskProgressionOutcome(
            completionEvent.Id,
            xpLedgerEntry?.Id,
            xpLedgerEntry?.XpGranted ?? 0,
            string.Equals(completionEvent.EventName, "TaskCompleted", StringComparison.Ordinal),
            idempotentReplay,
            idempotencyKey,
            streakEvaluation.Outcome,
            streakEvaluation.CurrentStreakDays,
            streakEvaluation.LongestStreakDays,
            timeZoneId,
            streakEvaluation.EvaluationWindowStartUtc,
            streakEvaluation.EvaluationWindowEndUtc);
    }

    private async Task<TaskProgressionOutcome> BuildReplayProgressionOutcomeAsync(
        TaskCompletionEvent completionEvent,
        XpLedgerEntry? xpLedgerEntry,
        string idempotencyKey,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.UserStreakSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(existingSnapshot => existingSnapshot.OwnerId == completionEvent.OwnerId, cancellationToken);

        if (snapshot is not null && snapshot.LastEvaluatedEventId == completionEvent.Id)
        {
            var streakFromSnapshot = new StreakEvaluationResult(
                snapshot.Outcome,
                snapshot.CurrentStreakDays,
                snapshot.LongestStreakDays,
                snapshot.EvaluationWindowStartUtc,
                snapshot.EvaluationWindowEndUtc);

            return BuildProgressionOutcome(
                completionEvent,
                xpLedgerEntry,
                idempotencyKey,
                streakFromSnapshot,
                snapshot.TimeZoneId,
                idempotentReplay: true);
        }

        var streakEvaluation = await EvaluateStreakAsync(
            completionEvent.OwnerId,
            completionEvent.OccurredAtUtc,
            completionEvent.ResultingIsCompleted,
            completionEvent.EventName,
            timeZoneId,
            cancellationToken);

        return BuildProgressionOutcome(
            completionEvent,
            xpLedgerEntry,
            idempotencyKey,
            streakEvaluation,
            timeZoneId,
            idempotentReplay: true);
    }

    private async Task<StreakEvaluationResult> EvaluateStreakAsync(
        Guid userId,
        DateTime occurredAtUtc,
        bool resultingIsCompleted,
        string eventName,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        var completionOccurredAtUtc = await dbContext.TaskCompletionEvents
            .AsNoTracking()
            .Where(completionEvent => completionEvent.OwnerId == userId && completionEvent.EventName == "TaskCompleted")
            .Select(completionEvent => completionEvent.OccurredAtUtc)
            .ToListAsync(cancellationToken);

        if (string.Equals(eventName, "TaskCompleted", StringComparison.Ordinal))
        {
            completionOccurredAtUtc.Add(occurredAtUtc);
        }

        return streakRuleEngine.Evaluate(
            timeZoneId,
            occurredAtUtc,
            resultingIsCompleted,
            completionOccurredAtUtc);
    }

    private async Task UpsertStreakSnapshotAsync(
        Guid ownerId,
        Guid completionEventId,
        string traceId,
        DateTime evaluatedAtUtc,
        string timeZoneId,
        StreakEvaluationResult streakEvaluation,
        CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.UserStreakSnapshots
            .FirstOrDefaultAsync(existingSnapshot => existingSnapshot.OwnerId == ownerId, cancellationToken);

        if (snapshot is null)
        {
            snapshot = new UserStreakSnapshot
            {
                OwnerId = ownerId
            };

            dbContext.UserStreakSnapshots.Add(snapshot);
        }

        snapshot.Outcome = streakEvaluation.Outcome;
        snapshot.CurrentStreakDays = streakEvaluation.CurrentStreakDays;
        snapshot.LongestStreakDays = streakEvaluation.LongestStreakDays;
        snapshot.TimeZoneId = timeZoneId;
        snapshot.EvaluationWindowStartUtc = streakEvaluation.EvaluationWindowStartUtc;
        snapshot.EvaluationWindowEndUtc = streakEvaluation.EvaluationWindowEndUtc;
        snapshot.LastEvaluatedEventId = completionEventId;
        snapshot.LastEvaluationTraceId = traceId;
        snapshot.LastEvaluatedAtUtc = evaluatedAtUtc;
    }

    private static bool TryResolveTimeZone(string timeZoneId, out string resolvedTimeZoneId)
    {
        resolvedTimeZoneId = string.Empty;

        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        try
        {
            var zone = TZConvert.GetTimeZoneInfo(timeZoneId);
            resolvedTimeZoneId = zone.Id;
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}