using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Features.Tasks.Repositories;
using TaskTracker.Api.Features.Tasks.Validation;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.AuthenticatedUser)]
[Route("api/v1/tasks")]
public class TasksController(
    ITaskRepository taskRepository,
    ILogger<TasksController> logger) : ControllerBase
{
    private static readonly HashSet<string> AllowedStates = new(StringComparer.Ordinal)
    {
        "active",
        "completed",
        "all"
    };

    private static readonly HashSet<string> AllowedEnergyLevels = new(StringComparer.Ordinal)
    {
        "low",
        "medium",
        "high"
    };

    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.Ordinal)
    {
        "low",
        "medium",
        "high"
    };

    [HttpGet]
    [ProducesResponseType<TaskListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List([FromQuery] TaskListQuery query, CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("tasks.identity.invalid");
        }

        if (!TryParseListFilters(query, out var state, out var title, out var priority, out var energyLevel, out var contextTag, out var errors))
        {
            return ValidationProblem("validation.request.invalid", errors);
        }

        var tasks = await taskRepository.ListOwnedByStateAsync(userId, state, title, priority, energyLevel, contextTag, cancellationToken);
        var (activeCount, completedCount) = await taskRepository.CountOwnedByCompletionStateAsync(userId, cancellationToken);

        var response = new TaskListResponse(
            tasks.Select(ToResponse).ToArray(),
            new TaskListSummaryResponse(activeCount, completedCount));

        return Ok(response);
    }

    [HttpPost]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("tasks.identity.invalid");
        }

        var (isValid, errors) = Validate(request);
        if (!isValid)
        {
            return ValidationProblem("validation.request.invalid", errors);
        }

        var now = DateTime.UtcNow;
        var normalizedPriority = request.Priority!.Trim().ToLowerInvariant();
        var normalizedCategory = request.Category!.Trim().ToLowerInvariant();
        var normalizedDifficulty = NormalizeDifficulty(request.Difficulty);
        var normalizedEnergyLevel = NormalizeEnergyLevel(request.EnergyLevel);
        var normalizedContextTag = NormalizeContextTag(request.ContextTag);

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title!.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            DueAtUtc = request.DueAtUtc,
            Priority = normalizedPriority,
            Category = normalizedCategory,
            Difficulty = normalizedDifficulty,
            EnergyLevel = normalizedEnergyLevel,
            ContextTag = normalizedContextTag,
            EffortPoints = request.EffortPoints,
            IsCompleted = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await taskRepository.CreateAsync(task, cancellationToken);

        logger.LogInformation(
            "Task {TaskId} created for user {UserId}. TraceId: {TraceId}",
            task.Id,
            userId,
            HttpContext.TraceIdentifier);

        return Created($"/api/v1/tasks/{task.Id}", ToResponse(task));
    }

    [HttpPut("{taskId}")]
    [ProducesResponseType<TaskResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] string taskId, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("tasks.identity.invalid");
        }

        if (!Guid.TryParse(taskId, out var parsedTaskId))
        {
            var routeErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["taskId"] = ["The taskId route value must be a valid GUID."]
            };

            return ValidationProblem("validation.request.invalid", routeErrors);
        }

        var (isValid, errors) = ValidateTaskPayload(
            request.Title,
            request.Description,
            request.DueAtUtc,
            request.Priority,
            request.Category,
            request.Difficulty,
            request.EnergyLevel,
            request.ContextTag,
            request.EffortPoints);

        if (!isValid)
        {
            return ValidationProblem("validation.request.invalid", errors);
        }

        var now = DateTime.UtcNow;
        var updateResult = await taskRepository.UpdateOwnedAsync(
            userId,
            parsedTaskId,
            request.Title!.Trim(),
            request.Description?.Trim() ?? string.Empty,
            request.DueAtUtc,
            request.Priority!.Trim().ToLowerInvariant(),
            request.Category!.Trim().ToLowerInvariant(),
            NormalizeDifficulty(request.Difficulty),
            NormalizeEnergyLevel(request.EnergyLevel),
            NormalizeContextTag(request.ContextTag),
            request.EffortPoints,
            now,
            cancellationToken);

        if (updateResult.Status == TaskUpdateStatus.Forbidden)
        {
            return ForbiddenProblem("auth.forbidden");
        }

        if (updateResult.Status == TaskUpdateStatus.NotFound)
        {
            return NotFoundProblem("tasks.not_found", "Task could not be found.");
        }

        logger.LogInformation(
            "Task {TaskId} updated for user {UserId}. TraceId: {TraceId}",
            parsedTaskId,
            userId,
            HttpContext.TraceIdentifier);

        return Ok(ToResponse(updateResult.Task!));
    }

    [HttpPatch("{taskId}/completion")]
    [ProducesResponseType<ToggleTaskCompletionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleCompletion(
        [FromRoute] string taskId,
        [FromBody] ToggleTaskCompletionRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("tasks.identity.invalid");
        }

        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!Guid.TryParse(taskId, out var parsedTaskId))
        {
            errors["taskId"] = ["The taskId route value must be a valid GUID."];
        }

        if (!request.IsCompleted.HasValue)
        {
            errors["isCompleted"] = ["The isCompleted field is required."];
        }

        if (!TryResolveIdempotencyKey(out var idempotencyKey, out var idempotencyError))
        {
            errors["idempotencyKey"] = [idempotencyError!];
        }

        if (errors.Count > 0)
        {
            return ValidationProblem("validation.request.invalid", errors);
        }

        var now = DateTime.UtcNow;
        var toggleResult = await taskRepository.ToggleCompletionOwnedAsync(
            userId,
            parsedTaskId,
            request.IsCompleted!.Value,
            idempotencyKey!,
            HttpContext.TraceIdentifier,
            now,
            cancellationToken);

        if (toggleResult.Status == TaskCompletionToggleStatus.Forbidden)
        {
            return ForbiddenProblem("auth.forbidden");
        }

        if (toggleResult.Status == TaskCompletionToggleStatus.NotFound)
        {
            return NotFoundProblem("tasks.not_found", "Task could not be found.");
        }

        if (toggleResult.Status == TaskCompletionToggleStatus.InvalidTimeZone)
        {
            var timeZoneErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["timeZoneId"] = ["The account timezone is invalid. Please update your account settings."]
            };

            return ValidationProblem("tasks.streak.timezone.invalid", timeZoneErrors);
        }

        var progression = toggleResult.ProgressionOutcome;
        var streak = progression is null
            ? new TaskCompletionStreakResponse(
                TaskStreakOutcome.Reset,
                0,
                0,
                "UTC",
                now,
                now)
            : new TaskCompletionStreakResponse(
                progression.StreakOutcome,
                progression.CurrentStreakDays,
                progression.LongestStreakDays,
                progression.TimeZoneId,
                progression.EvaluationWindowStartUtc,
                progression.EvaluationWindowEndUtc);

        var progressionResponse = new TaskCompletionProgressionResponse(
            progression?.CompletionEventId,
            progression?.XpLedgerEntryId,
            progression?.XpGranted ?? 0,
            progression?.EligibleForXp ?? false,
            progression?.IdempotentReplay ?? toggleResult.Status == TaskCompletionToggleStatus.IdempotentReplay,
            progression?.IdempotencyKey ?? idempotencyKey!,
            HttpContext.TraceIdentifier,
            streak);

        logger.LogInformation(
            "Task completion toggled for task {TaskId} by user {UserId}. EventId: {EventId}. XpGranted: {XpGranted}. Replay: {Replay}. TraceId: {TraceId}",
            parsedTaskId,
            userId,
            progressionResponse.CompletionEventId,
            progressionResponse.XpGranted,
            progressionResponse.IdempotentReplay,
            HttpContext.TraceIdentifier);

        return Ok(new ToggleTaskCompletionResponse(ToResponse(toggleResult.Task!), progressionResponse));
    }

    [HttpDelete("{taskId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete([FromRoute] string taskId, CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("tasks.identity.invalid");
        }

        if (!Guid.TryParse(taskId, out var parsedTaskId))
        {
            var routeErrors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
            {
                ["taskId"] = ["The taskId route value must be a valid GUID."]
            };

            return ValidationProblem("validation.request.invalid", routeErrors);
        }

        var deleteResult = await taskRepository.DeleteOwnedAsync(userId, parsedTaskId, cancellationToken);
        if (deleteResult.Status == TaskDeleteStatus.Forbidden)
        {
            return ForbiddenProblem("auth.forbidden");
        }

        if (deleteResult.Status == TaskDeleteStatus.CompletedTaskDeletionBlocked)
        {
            return ConflictProblem(
                "tasks.delete.completed.blocked",
                "Completed tasks cannot be deleted because progress must remain deterministic. Mark the task as active to adjust it, then keep it in completed history.");
        }

        logger.LogInformation(
            "Task {TaskId} delete processed for user {UserId} with status {DeleteStatus}. TraceId: {TraceId}",
            parsedTaskId,
            userId,
            deleteResult.Status,
            HttpContext.TraceIdentifier);

        return NoContent();
    }

    private bool TryResolveCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out userId);
    }

    private static (bool IsValid, Dictionary<string, string[]> Errors) Validate(CreateTaskRequest request)
    {
        return TaskPayloadValidator.Validate(
            request.Title,
            request.Description,
            request.DueAtUtc,
            request.Priority,
            request.Category,
            request.Difficulty,
            request.EnergyLevel,
            request.ContextTag,
            request.EffortPoints);
    }

    private static (bool IsValid, Dictionary<string, string[]> Errors) ValidateTaskPayload(
        string? title,
        string? description,
        DateTime? dueAtUtc,
        string? priority,
        string? category,
        string? difficulty,
        string? energyLevel,
        string? contextTag,
        int? effortPoints)
    {
        return TaskPayloadValidator.Validate(
            title,
            description,
            dueAtUtc,
            priority,
            category,
            difficulty,
            energyLevel,
            contextTag,
            effortPoints);
    }

    private static TaskResponse ToResponse(TaskItem task)
    {
        return new TaskResponse(
            task.Id,
            task.Title,
            task.Description,
            task.DueAtUtc,
            task.Priority,
            task.Category,
            task.Difficulty.ToString().ToLowerInvariant(),
            task.EnergyLevel.ToString().ToLowerInvariant(),
            task.ContextTag,
            task.EffortPoints,
            task.IsCompleted,
            task.CreatedAtUtc,
            task.UpdatedAtUtc);
    }

    private static TaskDifficulty NormalizeDifficulty(string? difficulty)
    {
        var normalizedDifficulty = string.IsNullOrWhiteSpace(difficulty)
            ? "easy"
            : difficulty.Trim().ToLowerInvariant();

        return normalizedDifficulty switch
        {
            "hard" => TaskDifficulty.Hard,
            "medium" => TaskDifficulty.Medium,
            _ => TaskDifficulty.Easy
        };
    }

    private static TaskEnergyLevel NormalizeEnergyLevel(string? energyLevel)
    {
        var normalizedEnergyLevel = string.IsNullOrWhiteSpace(energyLevel)
            ? "medium"
            : energyLevel.Trim().ToLowerInvariant();

        return normalizedEnergyLevel switch
        {
            "low" => TaskEnergyLevel.Low,
            "high" => TaskEnergyLevel.High,
            _ => TaskEnergyLevel.Medium
        };
    }

    private static string? NormalizeContextTag(string? contextTag)
    {
        if (string.IsNullOrWhiteSpace(contextTag))
        {
            return null;
        }

        return contextTag.Trim().ToLowerInvariant();
    }

    private static bool TryParseListFilters(
        TaskListQuery query,
        out TaskListState parsedState,
        out string? title,
        out string? priority,
        out string? energyLevel,
        out string? contextTag,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        title = null;
        priority = null;
        energyLevel = null;
        contextTag = null;

        var state = query.State;

        if (string.IsNullOrWhiteSpace(state))
        {
            parsedState = TaskListState.All;
        }
        else
        {
            var normalizedState = state.Trim().ToLowerInvariant();
            parsedState = normalizedState switch
            {
                "active" => TaskListState.Active,
                "completed" => TaskListState.Completed,
                "all" => TaskListState.All,
                _ => TaskListState.All
            };

            if (!AllowedStates.Contains(normalizedState))
            {
                errors["state"] = ["The state filter must be one of: active, completed, all."];
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Title))
        {
            var normalizedTitle = query.Title.Trim();
            if (normalizedTitle.Length > 160)
            {
                errors["title"] = ["The title filter must be 160 characters or fewer."];
            }
            else
            {
                title = normalizedTitle;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Priority))
        {
            var normalizedPriority = query.Priority.Trim().ToLowerInvariant();
            if (!AllowedPriorities.Contains(normalizedPriority))
            {
                errors["priority"] = ["The priority filter must be one of: low, medium, high."];
            }
            else
            {
                priority = normalizedPriority;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.EnergyLevel))
        {
            var normalizedEnergyLevel = query.EnergyLevel.Trim().ToLowerInvariant();
            if (!AllowedEnergyLevels.Contains(normalizedEnergyLevel))
            {
                errors["energyLevel"] = ["The energyLevel filter must be one of: low, medium, high."];
            }
            else
            {
                energyLevel = normalizedEnergyLevel;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.ContextTag))
        {
            var normalizedContextTag = query.ContextTag.Trim().ToLowerInvariant();
            if (normalizedContextTag.Length > 64)
            {
                errors["contextTag"] = ["The contextTag filter must be 64 characters or fewer."];
            }
            else
            {
                contextTag = normalizedContextTag;
            }
        }

        return errors.Count == 0;
    }

    private bool TryResolveIdempotencyKey(out string? idempotencyKey, out string? error)
    {
        idempotencyKey = null;
        error = null;

        if (!Request.Headers.TryGetValue("Idempotency-Key", out var values))
        {
            error = "Idempotency-Key header is required for completion toggle.";
            return false;
        }

        var normalized = values.FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "Idempotency-Key header is required for completion toggle.";
            return false;
        }

        if (!Guid.TryParse(normalized, out _))
        {
            error = "Idempotency-Key header must be a valid GUID.";
            return false;
        }

        idempotencyKey = normalized;
        return true;
    }

    private ObjectResult ValidationProblem(string code, Dictionary<string, string[]> errors)
    {
        var details = new ValidationProblemDetails(errors)
        {
            Type = "https://api.tasktracker.local/problems/validation",
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return BadRequest(details);
    }

    private ObjectResult UnauthorizedProblem(string code)
    {
        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/authentication-failed",
            Title = "Authentication Failed",
            Status = StatusCodes.Status401Unauthorized,
            Detail = "Authenticated user context is invalid."
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return StatusCode(StatusCodes.Status401Unauthorized, details);
    }

    private ObjectResult ForbiddenProblem(string code)
    {
        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/forbidden",
            Title = "Forbidden",
            Status = StatusCodes.Status403Forbidden
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return StatusCode(StatusCodes.Status403Forbidden, details);
    }

    private ObjectResult NotFoundProblem(string code, string detail)
    {
        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/not-found",
            Title = "Not Found",
            Status = StatusCodes.Status404NotFound,
            Detail = detail
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return StatusCode(StatusCodes.Status404NotFound, details);
    }

    private ObjectResult ConflictProblem(string code, string detail)
    {
        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/conflict",
            Title = "Conflict",
            Status = StatusCodes.Status409Conflict,
            Detail = detail
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return StatusCode(StatusCodes.Status409Conflict, details);
    }
}