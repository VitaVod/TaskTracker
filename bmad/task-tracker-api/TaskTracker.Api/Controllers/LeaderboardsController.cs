using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Leaderboards.Contracts;
using TaskTracker.Api.Features.Leaderboards.Repositories;
using TaskTracker.Api.Infrastructure.Authorization;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.AuthenticatedUser)]
[Route("api/v1/leaderboards")]
public class LeaderboardsController(
    ILeaderboardRepository leaderboardRepository,
    ILogger<LeaderboardsController> logger) : ControllerBase
{
    private const int MinPage = 1;
    private const int DefaultPage = 1;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;
    private const int MaxOffset = int.MaxValue;

    [HttpGet]
    [ProducesResponseType<LeaderboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromQuery] LeaderboardQuery query, CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("leaderboards.identity.invalid");
        }

        if (!await leaderboardRepository.UserExistsAsync(userId, cancellationToken))
        {
            return NotFoundProblem("leaderboards.user.not_found", "User account could not be found.");
        }

        if (!TryParseQuery(query, out var type, out var page, out var pageSize, out var errors))
        {
            return ValidationProblem("validation.request.invalid", errors);
        }

        var result = await leaderboardRepository.GetLeaderboardAsync(type, page, pageSize, cancellationToken);
        var hasNextPage = (result.Page * result.PageSize) < result.TotalCount;

        logger.LogInformation(
            "Leaderboard served. Type: {Type}. UserId: {UserId}. Page: {Page}. PageSize: {PageSize}. Total: {Total}. TraceId: {TraceId}",
            type,
            userId,
            result.Page,
            result.PageSize,
            result.TotalCount,
            HttpContext.TraceIdentifier);

        var response = new LeaderboardResponse(
            ToTypeValue(result.Type),
            result.Page,
            result.PageSize,
            result.TotalCount,
            hasNextPage,
            result.Items
                .Select(item => new LeaderboardEntryResponse(
                    item.Rank,
                    item.PublicIdentity,
                    ToIdentityModeValue(item.IdentityMode),
                    item.AvatarMarker,
                    item.MetricValue))
                .ToArray());

        return Ok(response);
    }

    private bool TryResolveCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out userId);
    }

    private static bool TryParseQuery(
        LeaderboardQuery query,
        out LeaderboardType type,
        out int page,
        out int pageSize,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!TryParseType(query.Type, out type))
        {
            errors["type"] = ["The type field is required and must be one of: streak, completedTasks."];
        }

        pageSize = query.PageSize ?? DefaultPageSize;
        if (pageSize < MinPageSize || pageSize > MaxPageSize)
        {
            errors["pageSize"] = [$"The pageSize field must be between {MinPageSize} and {MaxPageSize}."];
        }

        page = query.Page ?? DefaultPage;
        if (page < MinPage)
        {
            errors["page"] = [$"The page field must be greater than or equal to {MinPage}."];
        }
        else if (pageSize >= MinPageSize && pageSize <= MaxPageSize)
        {
            var maxPage = (MaxOffset / pageSize) + 1;
            if (page > maxPage)
            {
                errors["page"] = [$"The page field is too large for the selected pageSize. Maximum allowed page for pageSize {pageSize} is {maxPage}."];
            }
        }

        return errors.Count == 0;
    }

    private static bool TryParseType(string? value, out LeaderboardType type)
    {
        type = LeaderboardType.Streak;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (string.Equals(normalized, "streak", StringComparison.OrdinalIgnoreCase))
        {
            type = LeaderboardType.Streak;
            return true;
        }

        if (string.Equals(normalized, "completedTasks", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "completed", StringComparison.OrdinalIgnoreCase))
        {
            type = LeaderboardType.CompletedTasks;
            return true;
        }

        return false;
    }

    private static string ToTypeValue(LeaderboardType type)
    {
        return type == LeaderboardType.Streak ? "streak" : "completedTasks";
    }

    private static string ToIdentityModeValue(LeaderboardIdentityMode mode)
    {
        return mode == LeaderboardIdentityMode.Public ? "public" : "anonymous";
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