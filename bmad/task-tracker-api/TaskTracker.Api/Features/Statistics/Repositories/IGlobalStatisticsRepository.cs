namespace TaskTracker.Api.Features.Statistics.Repositories;

public interface IGlobalStatisticsRepository
{
    Task<(long TotalTasksCreated, long TotalTasksCompleted)> GetGlobalTaskStatisticsAsync(
        CancellationToken cancellationToken);
}
