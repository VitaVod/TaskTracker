using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Account.Repositories;
using TaskTracker.Api.Features.Notifications.Contracts;
using TaskTracker.Api.Features.Notifications.Validation;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.AuthenticatedUser)]
[Route("api/v1/notifications/preferences")]
public class NotificationPreferencesController(
    IAccountRepository accountRepository,
    INotificationPreferencesValidator notificationPreferencesValidator,
    ILogger<NotificationPreferencesController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<NotificationPreferencesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUserPreferences(CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("notifications.identity.invalid");
        }

        var user = await accountRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFoundProblem("notifications.user.not_found", "User account could not be found.");
        }

        return Ok(new NotificationPreferencesResponse(
            user.ReminderEmailEnabled,
            ToReminderCadenceValue(user.ReminderCadence),
            user.AccountEmailEnabled,
            user.ModifiedAtUtc));
    }

    [HttpPatch]
    [ProducesResponseType<NotificationPreferencesUpdateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCurrentUserPreferences([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("notifications.identity.invalid");
        }

        logger.LogInformation("Notification preferences update attempt for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

        var validationResult = notificationPreferencesValidator.ValidatePatch(payload);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("Notification preferences update rejected for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);
            return ValidationProblem("notifications.preferences.validation_failed", validationResult.Errors);
        }

        var user = await accountRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFoundProblem("notifications.user.not_found", "User account could not be found.");
        }

        var changed = ApplyPatch(user, validationResult);
        if (changed)
        {
            user.ModifiedAtUtc = DateTime.UtcNow;
            await accountRepository.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Notification preferences update completed for user {UserId}. Changed: {Changed}. TraceId: {TraceId}", userId, changed, HttpContext.TraceIdentifier);

        return Ok(new NotificationPreferencesUpdateResponse("Notification preferences updated successfully"));
    }

    private bool TryResolveCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out userId);
    }

    private static bool ApplyPatch(User user, NotificationPreferencesPatchValidationResult validationResult)
    {
        var changed = false;

        if (validationResult.HasReminderEmailEnabled && user.ReminderEmailEnabled != validationResult.ReminderEmailEnabled)
        {
            user.ReminderEmailEnabled = validationResult.ReminderEmailEnabled;
            changed = true;
        }

        if (validationResult.HasReminderCadence && user.ReminderCadence != validationResult.ReminderCadence)
        {
            user.ReminderCadence = validationResult.ReminderCadence;
            changed = true;
        }

        if (validationResult.HasAccountEmailEnabled && user.AccountEmailEnabled != validationResult.AccountEmailEnabled)
        {
            user.AccountEmailEnabled = validationResult.AccountEmailEnabled;
            changed = true;
        }

        return changed;
    }

    private static string ToReminderCadenceValue(NotificationReminderCadence cadence)
    {
        return cadence == NotificationReminderCadence.Weekly ? "weekly" : "daily";
    }

    private ObjectResult ValidationProblem(string code, Dictionary<string, string[]> errors)
    {
        var details = new ValidationProblemDetails(errors)
        {
            Type = "https://api.tasktracker.local/problems/validation-error",
            Title = "Validation Error",
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
