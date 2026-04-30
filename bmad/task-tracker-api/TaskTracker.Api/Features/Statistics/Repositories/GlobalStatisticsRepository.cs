using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Features.SharedViews.Caching;
using TaskTracker.Api.Infrastructure.Persistence;

namespace TaskTracker.Api.Features.Statistics.Repositories;

public class GlobalStatisticsRepository(
    TaskTrackerDbContext dbContext,
    ISharedViewCacheCoordinator sharedViewCache,
    ILogger<GlobalStatisticsRepository> logger) : IGlobalStatisticsRepository
{
    public async Task<(long TotalTasksCreated, long TotalTasksCompleted)> GetGlobalTaskStatisticsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            return await sharedViewCache.GetOrCreateGlobalStatisticsAsync(
                async ct =>
                {
                    var totalTasksCreated = await dbContext.Tasks
                        .AsNoTracking()
                        .LongCountAsync(ct);

                    var totalTasksCompleted = await dbContext.Tasks
                        .AsNoTracking()
                        .LongCountAsync(task => task.IsCompleted, ct);

                    return (totalTasksCreated, totalTasksCompleted);
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Shared view cache failed for global statistics read. Falling back to SQL query.");

            var totalTasksCreated = await dbContext.Tasks
                .AsNoTracking()
                .LongCountAsync(cancellationToken);

            var totalTasksCompleted = await dbContext.Tasks
                .AsNoTracking()
                .LongCountAsync(task => task.IsCompleted, cancellationToken);

            return (totalTasksCreated, totalTasksCompleted);
        }
    }
}
