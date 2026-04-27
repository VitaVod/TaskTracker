namespace TaskTracker.Api.Features.Tasks.Contracts;

public enum TaskListState
{
    Active,
    Completed,
    All
}

public record TaskListQuery(string? State);

public record CreateTaskRequest(
    string? Title,
    string? Description,
    DateTime? DueAtUtc,
    string? Priority,
    string? Category);

public record UpdateTaskRequest(
    string? Title,
    string? Description,
    DateTime? DueAtUtc,
    string? Priority,
    string? Category);

public record ToggleTaskCompletionRequest(bool? IsCompleted);

public record TaskResponse(
    Guid Id,
    string Title,
    string Description,
    DateTime? DueAtUtc,
    string Priority,
    string Category,
    bool IsCompleted,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public record TaskListSummaryResponse(
    int ActiveCount,
    int CompletedCount);

public record TaskListResponse(
    IReadOnlyCollection<TaskResponse> Items,
    TaskListSummaryResponse Summary);