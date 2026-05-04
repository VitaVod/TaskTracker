using System.Collections.Concurrent;
using System.Globalization;
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
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> IntegrationSyncLocks = new();

    public async Task CreateAsync(TaskItem task, CancellationToken cancellationToken)
    {
        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskItem>> ListOwnedByStateAsync(
        Guid userId,
        TaskListState state,
        string? title,
        string? priority,
        string? energyLevel,
        string? contextTag,
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

        if (!string.IsNullOrWhiteSpace(title))
        {
            query = query.Where(task => task.Title.Contains(title));
        }

        if (!string.IsNullOrWhiteSpace(priority))
        {
            query = query.Where(task => task.Priority == priority);
        }

        if (!string.IsNullOrWhiteSpace(energyLevel))
        {
            query = query.Where(task => task.EnergyLevel == ParseEnergyLevel(energyLevel));
        }

        if (!string.IsNullOrWhiteSpace(contextTag))
        {
            query = query.Where(task => task.ContextTag == contextTag);
        }

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
        TaskDifficulty difficulty,
        TaskEnergyLevel energyLevel,
        string? contextTag,
        int? effortPoints,
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
        task.Difficulty = difficulty;
        task.EnergyLevel = energyLevel;
        task.ContextTag = contextTag;
        task.EffortPoints = effortPoints;
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

            var completionEventName = stateChanged
                ? (isCompleted ? "TaskCompleted" : "TaskReopened")
                : "TaskCompletionSet";

            var completionEvent = new TaskCompletionEvent
            {
                Id = Guid.NewGuid(),
                TaskId = task.Id,
                OwnerId = task.UserId,
                EventName = completionEventName,
                ResultingIsCompleted = isCompleted,
                IdempotencyKey = idempotencyKey,
                OccurredAtUtc = updatedAtUtc,
                CreatedAtUtc = updatedAtUtc
            };

            dbContext.TaskCompletionEvents.Add(completionEvent);

            // Progression should react exactly once to true state transitions.
            var isEligibleForXp = stateChanged;
            XpLedgerEntry? xpLedgerEntry = null;
            if (isEligibleForXp)
            {
                var completionXpAmount = isCompleted
                    ? ResolveCompletionXpAmount(task.Difficulty)
                    : await ResolveReopenXpAmountAsync(task.UserId, task.Id, task.Difficulty, cancellationToken);

                xpLedgerEntry = new XpLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    OwnerId = task.UserId,
                    TaskId = task.Id,
                    TaskCompletionEventId = completionEvent.Id,
                    EventName = completionEvent.EventName,
                    IdempotencyKey = idempotencyKey,
                    XpGranted = isCompleted ? completionXpAmount : -completionXpAmount,
                    OccurredAtUtc = updatedAtUtc,
                    CreatedAtUtc = updatedAtUtc
                };

                dbContext.XpLedgerEntries.Add(xpLedgerEntry);
            }

            var existingSnapshot = await dbContext.UserStreakSnapshots
                .FirstOrDefaultAsync(snapshot => snapshot.OwnerId == userId, cancellationToken);

            var recoveryTokenState = ResolveRecoveryTokenState(
                existingSnapshot,
                completionEvent.OccurredAtUtc,
                resolvedTimeZoneId);

            var streakOutcome = await EvaluateStreakAsync(
                userId,
                task.Id,
                completionEvent.OccurredAtUtc,
                completionEvent.ResultingIsCompleted,
                completionEvent.EventName,
                recoveryTokenState.AvailableTokens,
                resolvedTimeZoneId,
                cancellationToken);

            UpsertStreakSnapshotAsync(
                existingSnapshot,
                userId,
                completionEvent.Id,
                traceId,
                updatedAtUtc,
                resolvedTimeZoneId,
                recoveryTokenState,
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

        if (task.IsCompleted)
        {
            return new TaskDeleteResult(TaskDeleteStatus.CompletedTaskDeletionBlocked);
        }

        dbContext.Tasks.Remove(task);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TaskDeleteResult(TaskDeleteStatus.Deleted);
    }

    public async Task<IntegrationTaskSyncResult> UpsertOwnedFromIntegrationAsync(
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
        CancellationToken cancellationToken)
    {
        idempotencyKey = NormalizeIntegrationIdempotencyKey(idempotencyKey);
        var lockKey = $"{ownerUserId:N}:{integrationId}:{idempotencyKey}";
        var syncLock = IntegrationSyncLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await syncLock.WaitAsync(cancellationToken);

        try
        {

        var existingReplay = await dbContext.IntegrationEventIdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(record =>
                record.OwnerUserId == ownerUserId
                && record.IntegrationId == integrationId
                && record.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existingReplay is not null)
        {
            var replayTask = await dbContext.Tasks
                .AsNoTracking()
                .FirstOrDefaultAsync(task => task.Id == existingReplay.TaskId && task.UserId == ownerUserId, cancellationToken);

            if (replayTask is null)
            {
                return new IntegrationTaskSyncResult(IntegrationTaskSyncStatus.Forbidden, null, null, null);
            }

            return new IntegrationTaskSyncResult(
                IntegrationTaskSyncStatus.IdempotentReplay,
                replayTask,
                existingReplay.Operation,
                existingReplay.ExternalTaskId);
        }

        var binding = await dbContext.IntegrationTaskSyncBindings
            .FirstOrDefaultAsync(existingBinding =>
                existingBinding.OwnerUserId == ownerUserId
                && existingBinding.IntegrationId == integrationId
                && existingBinding.ExternalTaskId == externalTaskId,
                cancellationToken);

        if (binding is null)
        {
            var createdTask = new TaskItem
            {
                Id = Guid.NewGuid(),
                UserId = ownerUserId,
                Title = title,
                Description = description,
                DueAtUtc = dueAtUtc,
                Priority = priority,
                Category = category,
                Difficulty = difficulty,
                EnergyLevel = energyLevel,
                ContextTag = contextTag,
                EffortPoints = effortPoints,
                IsCompleted = isCompleted,
                CreatedAtUtc = updatedAtUtc,
                UpdatedAtUtc = updatedAtUtc
            };

            var createdBinding = new IntegrationTaskSyncBinding
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                IntegrationId = integrationId,
                ExternalTaskId = externalTaskId,
                TaskId = createdTask.Id,
                CreatedAtUtc = updatedAtUtc,
                UpdatedAtUtc = updatedAtUtc
            };

            dbContext.Tasks.Add(createdTask);
            dbContext.IntegrationTaskSyncBindings.Add(createdBinding);

            var createRecord = new IntegrationEventIdempotencyRecord
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                IntegrationId = integrationId,
                IdempotencyKey = idempotencyKey,
                TaskId = createdTask.Id,
                ExternalTaskId = externalTaskId,
                Operation = "created",
                CorrelationId = correlationId,
                TraceId = traceId,
                ProcessedAtUtc = updatedAtUtc,
                CreatedAtUtc = updatedAtUtc
            };

            dbContext.IntegrationEventIdempotencyRecords.Add(createRecord);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return new IntegrationTaskSyncResult(
                    IntegrationTaskSyncStatus.Created,
                    createdTask,
                    "created",
                    externalTaskId);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                dbContext.ChangeTracker.Clear();

                var replay = await dbContext.IntegrationEventIdempotencyRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(record =>
                        record.OwnerUserId == ownerUserId
                        && record.IntegrationId == integrationId
                        && record.IdempotencyKey == idempotencyKey,
                        cancellationToken);

                if (replay is not null)
                {
                    var replayTask = await dbContext.Tasks
                        .AsNoTracking()
                        .FirstOrDefaultAsync(task => task.Id == replay.TaskId && task.UserId == ownerUserId, cancellationToken);

                    if (replayTask is not null)
                    {
                        return new IntegrationTaskSyncResult(
                            IntegrationTaskSyncStatus.IdempotentReplay,
                            replayTask,
                            replay.Operation,
                            replay.ExternalTaskId);
                    }
                }

                binding = await dbContext.IntegrationTaskSyncBindings
                    .FirstOrDefaultAsync(existingBinding =>
                        existingBinding.OwnerUserId == ownerUserId
                        && existingBinding.IntegrationId == integrationId
                        && existingBinding.ExternalTaskId == externalTaskId,
                        cancellationToken);

                if (binding is null)
                {
                    throw;
                }
            }
        }

        var task = await dbContext.Tasks.FirstOrDefaultAsync(existingTask => existingTask.Id == binding.TaskId, cancellationToken);
        if (task is null)
        {
            var recoveredTask = new TaskItem
            {
                Id = Guid.NewGuid(),
                UserId = ownerUserId,
                Title = title,
                Description = description,
                DueAtUtc = dueAtUtc,
                Priority = priority,
                Category = category,
                Difficulty = difficulty,
                EnergyLevel = energyLevel,
                ContextTag = contextTag,
                EffortPoints = effortPoints,
                IsCompleted = isCompleted,
                CreatedAtUtc = updatedAtUtc,
                UpdatedAtUtc = updatedAtUtc
            };

            binding.TaskId = recoveredTask.Id;
            binding.UpdatedAtUtc = updatedAtUtc;

            dbContext.Tasks.Add(recoveredTask);

            dbContext.IntegrationEventIdempotencyRecords.Add(new IntegrationEventIdempotencyRecord
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                IntegrationId = integrationId,
                IdempotencyKey = idempotencyKey,
                TaskId = recoveredTask.Id,
                ExternalTaskId = externalTaskId,
                Operation = "created",
                CorrelationId = correlationId,
                TraceId = traceId,
                ProcessedAtUtc = updatedAtUtc,
                CreatedAtUtc = updatedAtUtc
            });

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return new IntegrationTaskSyncResult(
                    IntegrationTaskSyncStatus.Created,
                    recoveredTask,
                    "created",
                    externalTaskId);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                dbContext.ChangeTracker.Clear();

                var replay = await dbContext.IntegrationEventIdempotencyRecords
                    .AsNoTracking()
                    .FirstOrDefaultAsync(record =>
                        record.OwnerUserId == ownerUserId
                        && record.IntegrationId == integrationId
                        && record.IdempotencyKey == idempotencyKey,
                        cancellationToken);

                if (replay is not null)
                {
                    var replayTask = await dbContext.Tasks
                        .AsNoTracking()
                        .FirstOrDefaultAsync(existingTask => existingTask.Id == replay.TaskId && existingTask.UserId == ownerUserId, cancellationToken);

                    if (replayTask is not null)
                    {
                        return new IntegrationTaskSyncResult(
                            IntegrationTaskSyncStatus.IdempotentReplay,
                            replayTask,
                            replay.Operation,
                            replay.ExternalTaskId);
                    }
                }

                throw;
            }
        }

        if (task.UserId != ownerUserId)
        {
            return new IntegrationTaskSyncResult(IntegrationTaskSyncStatus.Forbidden, null, null, null);
        }

        task.Title = title;
        task.Description = description;
        task.DueAtUtc = dueAtUtc;
        task.Priority = priority;
        task.Category = category;
        task.Difficulty = difficulty;
        task.EnergyLevel = energyLevel;
        task.ContextTag = contextTag;
        task.EffortPoints = effortPoints;
        task.IsCompleted = isCompleted;
        task.UpdatedAtUtc = updatedAtUtc;

        binding.UpdatedAtUtc = updatedAtUtc;

        dbContext.IntegrationEventIdempotencyRecords.Add(new IntegrationEventIdempotencyRecord
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            IntegrationId = integrationId,
            IdempotencyKey = idempotencyKey,
            TaskId = task.Id,
            ExternalTaskId = externalTaskId,
            Operation = "updated",
            CorrelationId = correlationId,
            TraceId = traceId,
            ProcessedAtUtc = updatedAtUtc,
            CreatedAtUtc = updatedAtUtc
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new IntegrationTaskSyncResult(
                IntegrationTaskSyncStatus.Updated,
                task,
                "updated",
                externalTaskId);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            dbContext.ChangeTracker.Clear();

            var replay = await dbContext.IntegrationEventIdempotencyRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(record =>
                    record.OwnerUserId == ownerUserId
                    && record.IntegrationId == integrationId
                    && record.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (replay is not null)
            {
                var replayTask = await dbContext.Tasks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(existingTask => existingTask.Id == replay.TaskId && existingTask.UserId == ownerUserId, cancellationToken);

                if (replayTask is not null)
                {
                    return new IntegrationTaskSyncResult(
                        IntegrationTaskSyncStatus.IdempotentReplay,
                        replayTask,
                        replay.Operation,
                        replay.ExternalTaskId);
                }
            }

            throw;
        }
        }
        finally
        {
            syncLock.Release();

            // Keep lock registry bounded when there is no queued waiter.
            if (syncLock.CurrentCount == 1)
            {
                IntegrationSyncLocks.TryRemove(lockKey, out _);
            }
        }
    }

    private static string NormalizeIntegrationIdempotencyKey(string idempotencyKey)
    {
        var trimmed = idempotencyKey.Trim();
        return Guid.TryParse(trimmed, out var parsed)
            ? parsed.ToString("D")
            : trimmed;
    }

    private static TaskEnergyLevel ParseEnergyLevel(string energyLevel)
    {
        return energyLevel.Trim().ToLowerInvariant() switch
        {
            "low" => TaskEnergyLevel.Low,
            "high" => TaskEnergyLevel.High,
            _ => TaskEnergyLevel.Medium
        };
    }

    private async Task<int> ResolveReopenXpAmountAsync(
        Guid userId,
        Guid taskId,
        TaskDifficulty fallbackDifficulty,
        CancellationToken cancellationToken)
    {
        var lastAwardedXp = await dbContext.XpLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == userId
                && entry.TaskId == taskId
                && entry.XpGranted > 0)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.CreatedAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => (int?)entry.XpGranted)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastAwardedXp is > 0)
        {
            return lastAwardedXp.Value;
        }

        // Fall back to deterministic mapping if historical positive award is unavailable.
        return ResolveCompletionXpAmount(fallbackDifficulty);
    }

    private static int ResolveCompletionXpAmount(TaskDifficulty difficulty)
    {
        return difficulty switch
        {
            TaskDifficulty.Hard => 30,
            TaskDifficulty.Medium => 20,
            _ => 10
        };
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
            xpLedgerEntry is not null,
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
                snapshot.EvaluationWindowEndUtc,
                RecoveryTokenConsumed: false,
                RemainingRecoveryTokens: snapshot.RecoveryTokenBalance);

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
            completionEvent.TaskId,
            completionEvent.OccurredAtUtc,
            completionEvent.ResultingIsCompleted,
            completionEvent.EventName,
            availableRecoveryTokens: 0,
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
        Guid taskId,
        DateTime occurredAtUtc,
        bool resultingIsCompleted,
        string eventName,
        int availableRecoveryTokens,
        string timeZoneId,
        CancellationToken cancellationToken)
    {
        var completionEvents = await dbContext.TaskCompletionEvents
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

        completionEvents.Add(new CompletionStateEvent(
            taskId,
            eventName,
            resultingIsCompleted,
            occurredAtUtc,
            occurredAtUtc,
            Guid.Empty));

        var effectiveCompletionOccurredAtUtc = BuildEffectiveCompletionOccurredAtUtc(completionEvents);

        return streakRuleEngine.Evaluate(
            timeZoneId,
            occurredAtUtc,
            resultingIsCompleted,
            effectiveCompletionOccurredAtUtc,
            availableRecoveryTokens);
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

    private sealed record CompletionStateEvent(
        Guid TaskId,
        string EventName,
        bool ResultingIsCompleted,
        DateTime OccurredAtUtc,
        DateTime CreatedAtUtc,
        Guid EventId);

    private void UpsertStreakSnapshotAsync(
        UserStreakSnapshot? snapshot,
        Guid ownerId,
        Guid completionEventId,
        string traceId,
        DateTime evaluatedAtUtc,
        string timeZoneId,
        RecoveryTokenState recoveryTokenState,
        StreakEvaluationResult streakEvaluation,
        CancellationToken cancellationToken)
    {
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
        snapshot.RecoveryTokenBalance = streakEvaluation.RemainingRecoveryTokens;
        snapshot.RecoveryTokenWeekKey = recoveryTokenState.WeekKey;
        if (recoveryTokenState.GrantedThisEvaluation)
        {
            snapshot.LastRecoveryTokenGrantedAtUtc = evaluatedAtUtc;
            dbContext.StreakRecoveryTokenEvents.Add(new StreakRecoveryTokenEvent
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                EventType = StreakRecoveryTokenEventType.Granted,
                TimeZoneId = timeZoneId,
                LocalDate = recoveryTokenState.LocalDate,
                WeekKey = recoveryTokenState.WeekKey,
                BalanceAfter = recoveryTokenState.AvailableTokens,
                OccurredAtUtc = evaluatedAtUtc,
                CompletionEventId = completionEventId,
                TraceId = traceId
            });
        }

        if (streakEvaluation.RecoveryTokenConsumed)
        {
            snapshot.LastRecoveryTokenConsumedAtUtc = evaluatedAtUtc;
            dbContext.StreakRecoveryTokenEvents.Add(new StreakRecoveryTokenEvent
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                EventType = StreakRecoveryTokenEventType.Consumed,
                TimeZoneId = timeZoneId,
                LocalDate = recoveryTokenState.LocalDate,
                WeekKey = recoveryTokenState.WeekKey,
                BalanceAfter = streakEvaluation.RemainingRecoveryTokens,
                OccurredAtUtc = evaluatedAtUtc,
                CompletionEventId = completionEventId,
                TraceId = traceId
            });
        }

        snapshot.LastEvaluatedEventId = completionEventId;
        snapshot.LastEvaluationTraceId = traceId;
        snapshot.LastEvaluatedAtUtc = evaluatedAtUtc;
    }

    private static RecoveryTokenState ResolveRecoveryTokenState(
        UserStreakSnapshot? snapshot,
        DateTime evaluationOccurredAtUtc,
        string timeZoneId)
    {
        var timeZone = TZConvert.GetTimeZoneInfo(timeZoneId);
        var evaluationLocalDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(evaluationOccurredAtUtc), timeZone));
        var weekKey = BuildIsoWeekKey(evaluationLocalDate);
        var previousWeekKey = snapshot?.RecoveryTokenWeekKey ?? string.Empty;
        var grantedThisEvaluation = !string.Equals(previousWeekKey, weekKey, StringComparison.Ordinal);
        var availableTokens = grantedThisEvaluation
            ? 1
            : Math.Clamp(snapshot?.RecoveryTokenBalance ?? 0, 0, 1);

        return new RecoveryTokenState(
            availableTokens,
            grantedThisEvaluation,
            weekKey,
            evaluationLocalDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    private static string BuildIsoWeekKey(DateOnly localDate)
    {
        var dateTime = localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var isoYear = ISOWeek.GetYear(dateTime);
        var isoWeek = ISOWeek.GetWeekOfYear(dateTime);
        return $"{isoYear:D4}-W{isoWeek:D2}";
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    private sealed record RecoveryTokenState(
        int AvailableTokens,
        bool GrantedThisEvaluation,
        string WeekKey,
        string LocalDate);

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