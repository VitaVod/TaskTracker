using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Account.Repositories;
using TaskTracker.Api.Infrastructure.Authorization;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api/v1/ops")]
public class OperationsController(IAccountRepository accountRepository) : ControllerBase
{
    [HttpGet("admin/health")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public IActionResult GetAdminHealth()
    {
        return Ok(new
        {
            status = "ok",
            capability = "admin",
            traceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("support/users/{userId:guid}")]
    [Authorize(Policy = AppPolicies.SupportOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupportUserSnapshot([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        var user = await accountRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            var details = new ProblemDetails
            {
                Type = "https://api.tasktracker.local/problems/not-found",
                Title = "Not Found",
                Status = StatusCodes.Status404NotFound,
                Detail = "User account could not be found."
            };

            details.Extensions["code"] = "account.user.not_found";
            details.Extensions["traceId"] = HttpContext.TraceIdentifier;

            return StatusCode(StatusCodes.Status404NotFound, details);
        }

        return Ok(new
        {
            user.Id,
            user.Email,
            user.DisplayName,
            user.TimeZoneId,
            user.Locale,
            user.ModifiedAtUtc
        });
    }
}
