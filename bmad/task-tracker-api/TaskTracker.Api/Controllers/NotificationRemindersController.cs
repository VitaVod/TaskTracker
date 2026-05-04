using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Notifications.Contracts;
using TaskTracker.Api.Features.Notifications.Reminders;
using TaskTracker.Api.Infrastructure.Authorization;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.AdminOnly)]
[Route("api/v1/internal/notifications/reminders")]
public class NotificationRemindersController(
    IReminderProcessingService reminderProcessingService,
    ILogger<NotificationRemindersController> logger) : ControllerBase
{
    [HttpPost("run")]
    [ProducesResponseType<ReminderProcessingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Run(CancellationToken cancellationToken)
    {
        logger.LogInformation("Reminder processing requested. TraceId={TraceId}", HttpContext.TraceIdentifier);

        var result = await reminderProcessingService.ProcessAsync(HttpContext.TraceIdentifier, cancellationToken);

        return Ok(new ReminderProcessingResponse(
            result.StartedAtUtc,
            result.CompletedAtUtc,
            result.EligibleUserCount,
            result.ProcessedUserCount,
            result.SentCount,
            result.SkippedCount,
            result.FailedCount));
    }
}
