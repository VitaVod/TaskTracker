namespace TaskTracker.Api.Features.Statistics.Contracts;

public record GlobalStatisticsResponse(
    long TotalTasksCreated,
    long TotalTasksCompleted);
