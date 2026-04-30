using System.Text.Json;
using System.Text.Json.Serialization;

namespace TaskTracker.Api.Features.Tasks.Contracts;

[JsonConverter(typeof(TaskStreakOutcomeJsonConverter))]
public enum TaskStreakOutcome
{
    Continue,
    Reset,
    Restart
}

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

public record TaskCompletionProgressionResponse(
    Guid? CompletionEventId,
    Guid? XpLedgerEntryId,
    int XpGranted,
    bool EligibleForXp,
    bool IdempotentReplay,
    string IdempotencyKey,
    string TraceId,
    TaskCompletionStreakResponse Streak);

public record TaskCompletionStreakResponse(
    TaskStreakOutcome Outcome,
    int CurrentStreakDays,
    int LongestStreakDays,
    string TimeZoneId,
    DateTime EvaluationWindowStartUtc,
    DateTime EvaluationWindowEndUtc);

public record ToggleTaskCompletionResponse(
    TaskResponse Task,
    TaskCompletionProgressionResponse Progression);

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

public sealed class TaskStreakOutcomeJsonConverter : JsonConverter<TaskStreakOutcome>
{
    public override TaskStreakOutcome Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value switch
        {
            "continue" => TaskStreakOutcome.Continue,
            "reset" => TaskStreakOutcome.Reset,
            "restart" => TaskStreakOutcome.Restart,
            _ => throw new JsonException($"Unsupported streak outcome value '{value}'.")
        };
    }

    public override void Write(Utf8JsonWriter writer, TaskStreakOutcome value, JsonSerializerOptions options)
    {
        var serialized = value switch
        {
            TaskStreakOutcome.Continue => "continue",
            TaskStreakOutcome.Reset => "reset",
            TaskStreakOutcome.Restart => "restart",
            _ => throw new JsonException($"Unsupported streak outcome value '{value}'.")
        };

        writer.WriteStringValue(serialized);
    }
}