using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Features.Tasks.Repositories;
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

        if (!TryParseState(query.State, out var state, out var errors))
        {
            return ValidationProblem("validation.request.invalid", errors);
        }

        var tasks = await taskRepository.ListOwnedByStateAsync(userId, state, cancellationToken);
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

        var task = new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title!.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            DueAtUtc = request.DueAtUtc,
            Priority = normalizedPriority,
            Category = normalizedCategory,
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
            request.Category);

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

    private bool TryResolveCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out userId);
    }

    private static (bool IsValid, Dictionary<string, string[]> Errors) Validate(CreateTaskRequest request)
    {
        return ValidateTaskPayload(
            request.Title,
            request.Description,
            request.DueAtUtc,
            request.Priority,
            request.Category);
    }

    private static (bool IsValid, Dictionary<string, string[]> Errors) ValidateTaskPayload(
        string? title,
        string? description,
        DateTime? dueAtUtc,
        string? priority,
        string? category)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["The title field is required."];
        }
        else if (title.Trim().Length > 160)
        {
            errors["title"] = ["The title field must be 160 characters or fewer."];
        }

        var normalizedDescription = description?.Trim() ?? string.Empty;
        if (normalizedDescription.Length > 2000)
        {
            errors["description"] = ["The description field must be 2000 characters or fewer."];
        }

        if (dueAtUtc.HasValue && dueAtUtc.Value.Kind != DateTimeKind.Utc)
        {
            errors["dueAtUtc"] = ["The dueAtUtc field must be a UTC datetime value."];
        }

        if (string.IsNullOrWhiteSpace(priority))
        {
            errors["priority"] = ["The priority field is required."];
        }
        else if (!AllowedPriorities.Contains(priority.Trim().ToLowerInvariant()))
        {
            errors["priority"] = ["The priority field must be one of: low, medium, high."];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors["category"] = ["The category field is required."];
        }
        else if (category.Trim().Length > 64)
        {
            errors["category"] = ["The category field must be 64 characters or fewer."];
        }

        return (errors.Count == 0, errors);
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
            task.IsCompleted,
            task.CreatedAtUtc,
            task.UpdatedAtUtc);
    }

    private static bool TryParseState(string? state, out TaskListState parsedState, out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(state))
        {
            parsedState = TaskListState.All;
            return true;
        }

        var normalizedState = state.Trim().ToLowerInvariant();
        parsedState = normalizedState switch
        {
            "active" => TaskListState.Active,
            "completed" => TaskListState.Completed,
            "all" => TaskListState.All,
            _ => TaskListState.All
        };

        if (AllowedStates.Contains(normalizedState))
        {
            return true;
        }

        errors["state"] = ["The state filter must be one of: active, completed, all."];
        return false;
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
}