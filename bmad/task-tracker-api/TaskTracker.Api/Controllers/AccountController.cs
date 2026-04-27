using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Account.Contracts;
using TaskTracker.Api.Features.Account.Repositories;
using TaskTracker.Api.Features.Account.Validation;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.AuthenticatedUser)]
[Route("api/v1/account")]
public class AccountController(
    IAccountRepository accountRepository,
    IAccountUpdateValidator accountUpdateValidator,
    ILogger<AccountController> logger) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<AccountMeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("account.identity.invalid");
        }

        var user = await accountRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFoundProblem("account.user.not_found", "User account could not be found.");
        }

        return Ok(new AccountMeResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.TimeZoneId,
            user.Locale,
            user.ModifiedAtUtc));
    }

    [HttpPatch("profile")]
    [ProducesResponseType<AccountUpdateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("account.identity.invalid");
        }

        logger.LogInformation("Profile update attempt for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

        var validationResult = accountUpdateValidator.ValidateProfilePatch(payload);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("Profile update rejected for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);
            return ValidationProblem("account.profile.validation_failed", validationResult.Errors);
        }

        var user = await accountRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFoundProblem("account.user.not_found", "User account could not be found.");
        }

        var changed = ApplyProfilePatch(user, validationResult);
        if (changed)
        {
            user.ModifiedAtUtc = DateTime.UtcNow;
            await accountRepository.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Profile update completed for user {UserId}. Changed: {Changed}. TraceId: {TraceId}", userId, changed, HttpContext.TraceIdentifier);

        return Ok(new AccountUpdateResponse("Profile updated successfully"));
    }

    [HttpPatch("settings")]
    [ProducesResponseType<AccountUpdateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateSettings([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("account.identity.invalid");
        }

        logger.LogInformation("Settings update attempt for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);

        var validationResult = accountUpdateValidator.ValidateSettingsPatch(payload);
        if (!validationResult.IsValid)
        {
            logger.LogWarning("Settings update rejected for user {UserId}. TraceId: {TraceId}", userId, HttpContext.TraceIdentifier);
            return ValidationProblem("account.settings.validation_failed", validationResult.Errors);
        }

        var user = await accountRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFoundProblem("account.user.not_found", "User account could not be found.");
        }

        var changed = ApplySettingsPatch(user, validationResult);
        if (changed)
        {
            user.ModifiedAtUtc = DateTime.UtcNow;
            await accountRepository.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Settings update completed for user {UserId}. Changed: {Changed}. TraceId: {TraceId}", userId, changed, HttpContext.TraceIdentifier);

        return Ok(new AccountUpdateResponse("Settings updated successfully"));
    }

    [HttpGet("users/{userId:guid}")]
    [Authorize(Policy = AppPolicies.AccountOwnerOrPrivileged)]
    [ProducesResponseType<AccountMeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById([FromRoute] Guid userId, CancellationToken cancellationToken)
    {
        var user = await accountRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFoundProblem("account.user.not_found", "User account could not be found.");
        }

        return Ok(new AccountMeResponse(
            user.Id,
            user.Email,
            user.DisplayName,
            user.TimeZoneId,
            user.Locale,
            user.ModifiedAtUtc));
    }

    private bool TryResolveCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out userId);
    }

    private static bool ApplyProfilePatch(User user, AccountProfilePatchValidationResult validationResult)
    {
        var changed = false;

        if (validationResult.HasDisplayName && !string.Equals(user.DisplayName, validationResult.DisplayName, StringComparison.Ordinal))
        {
            user.DisplayName = validationResult.DisplayName;
            changed = true;
        }

        return changed;
    }

    private static bool ApplySettingsPatch(User user, AccountSettingsPatchValidationResult validationResult)
    {
        var changed = false;

        if (validationResult.HasTimeZoneId && !string.Equals(user.TimeZoneId, validationResult.TimeZoneId, StringComparison.Ordinal))
        {
            user.TimeZoneId = validationResult.TimeZoneId;
            changed = true;
        }

        if (validationResult.HasLocale && !string.Equals(user.Locale, validationResult.Locale, StringComparison.Ordinal))
        {
            user.Locale = validationResult.Locale;
            changed = true;
        }

        return changed;
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
