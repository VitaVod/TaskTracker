using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Tasks.Repositories;

public class TaskRepository(TaskTrackerDbContext dbContext) : ITaskRepository
{
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
}