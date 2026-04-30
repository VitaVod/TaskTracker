using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Statistics.Contracts;
using TaskTracker.Api.Features.Statistics.Repositories;
using TaskTracker.Api.Infrastructure.Authorization;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.AuthenticatedUser)]
[Route("api/v1/statistics")]
public class StatisticsController(
    IGlobalStatisticsRepository globalStatisticsRepository,
    ILogger<StatisticsController> logger) : ControllerBase
{
    [HttpGet("global")]
    [ProducesResponseType<GlobalStatisticsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGlobal(CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("statistics.identity.invalid");
        }

        var (totalTasksCreated, totalTasksCompleted) = await globalStatisticsRepository
            .GetGlobalTaskStatisticsAsync(cancellationToken);

        logger.LogInformation(
            "Global task statistics served. UserId: {UserId}. TotalTasksCreated: {TotalTasksCreated}. TotalTasksCompleted: {TotalTasksCompleted}. TraceId: {TraceId}",
            userId,
            totalTasksCreated,
            totalTasksCompleted,
            HttpContext.TraceIdentifier);

        return Ok(new GlobalStatisticsResponse(totalTasksCreated, totalTasksCompleted));
    }

    private bool TryResolveCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out userId);
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
}
