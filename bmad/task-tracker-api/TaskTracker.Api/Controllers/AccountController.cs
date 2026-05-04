using System.Security.Claims;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Account.Contracts;
using TaskTracker.Api.Features.Account.Repositories;
using TaskTracker.Api.Features.Auth.Security;
using TaskTracker.Api.Features.Notifications.AccountEvents;
using TaskTracker.Api.Features.Account.Validation;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.AuthenticatedUser)]
[Route("api/v1/account")]
public class AccountController(
    IAccountRepository accountRepository,
    IPasswordHasher passwordHasher,
    IAccountEventNotificationService accountEventNotificationService,
    IAccountUpdateValidator accountUpdateValidator,
    ILogger<AccountController> logger) : ControllerBase
{
    private static readonly TimeSpan EmailChangeTokenLifetime = TimeSpan.FromMinutes(30);

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
            ToParticipationModeValue(user.LeaderboardParticipationMode),
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

    [HttpPost("email-change/request")]
    [ProducesResponseType<AccountEmailChangeRequestResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestEmailChange(
        [FromBody] AccountEmailChangeRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("account.identity.invalid");
        }

        var validationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var normalizedNewEmail = (request.NewEmail ?? string.Empty).Trim().ToLowerInvariant();

        if (!IsEmailValid(normalizedNewEmail))
        {
            validationErrors["newEmail"] = ["A valid email address is required."];
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
        {
            validationErrors["currentPassword"] = ["Current password is required."];
        }

        if (validationErrors.Count > 0)
        {
            return ValidationProblem("account.email-change.validation_failed", validationErrors);
        }

        var user = await accountRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFoundProblem("account.user.not_found", "User account could not be found.");
        }

        if (string.Equals(user.Email, normalizedNewEmail, StringComparison.Ordinal))
        {
            return ValidationProblem("account.email-change.validation_failed", new Dictionary<string, string[]>
            {
                ["newEmail"] = ["New email must be different from your current email."]
            });
        }

        if (!passwordHasher.Verify(request.CurrentPassword, user.PasswordHash, user.PasswordSalt))
        {
            return ValidationProblem("account.email-change.validation_failed", new Dictionary<string, string[]>
            {
                ["currentPassword"] = ["Current password is incorrect."]
            });
        }

        var existingUser = await accountRepository.FindUserByEmailAsync(normalizedNewEmail, cancellationToken);
        if (existingUser is null)
        {
            var issued = await accountRepository.IssueEmailChangeTokenAsync(
                user.Id,
                normalizedNewEmail,
                EmailChangeTokenLifetime,
                cancellationToken);

            await accountEventNotificationService.NotifyEmailChangeRequestedAsync(
                user.Id,
                issued.NewEmail,
                issued.TokenId,
                BuildEmailChangeConfirmationLink(issued.PlainTextToken),
                issued.ExpiresAtUtc,
                HttpContext.TraceIdentifier,
                cancellationToken);
        }

        return Accepted(new AccountEmailChangeRequestResponse(
            "If the email can be changed, a confirmation link has been sent."));
    }

    [AllowAnonymous]
    [HttpPost("email-change/confirm")]
    [ProducesResponseType<AccountEmailChangeConfirmResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmEmailChange(
        [FromBody] AccountEmailChangeConfirmRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return EmailChangeInvalidProblem();
        }

        var result = await accountRepository.ConfirmEmailChangeAsync(request.Token, cancellationToken);
        if (result.Outcome == ConfirmEmailChangeOutcome.Success)
        {
            await accountEventNotificationService.NotifyEmailChangeCompletedAsync(
                result.UserId!.Value,
                result.PreviousEmail!,
                result.NewEmail!,
                HttpContext.TraceIdentifier,
                cancellationToken);

            return Ok(new AccountEmailChangeConfirmResponse("Email updated successfully"));
        }

        return EmailChangeInvalidProblem();
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
            ToParticipationModeValue(user.LeaderboardParticipationMode),
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

        if (validationResult.HasLeaderboardParticipationMode && user.LeaderboardParticipationMode != validationResult.LeaderboardParticipationMode)
        {
            user.LeaderboardParticipationMode = validationResult.LeaderboardParticipationMode;
            changed = true;
        }

        return changed;
    }

    private static string ToParticipationModeValue(LeaderboardParticipationMode mode)
    {
        return mode == LeaderboardParticipationMode.Public
            ? "public"
            : mode == LeaderboardParticipationMode.Anonymous
                ? "anonymous"
                : "hidden";
    }

    private string BuildEmailChangeConfirmationLink(string rawToken)
    {
        var encodedToken = Uri.EscapeDataString(rawToken);
        var host = Request.Host.HasValue ? Request.Host.Value : "app.tasktracker.local";
        var scheme = string.IsNullOrWhiteSpace(Request.Scheme) ? "https" : Request.Scheme;
        return $"{scheme}://{host}/account/confirm-email-change?token={encodedToken}";
    }

    private static bool IsEmailValid(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
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

    private ObjectResult EmailChangeInvalidProblem()
    {
        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/email-change-invalid",
            Title = "Email Change Link Invalid",
            Status = StatusCodes.Status400BadRequest,
            Detail = "This email confirmation link is expired, invalid, or already used. Request a new email change."
        };

        details.Extensions["code"] = "account.email-change.invalid";
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return BadRequest(details);
    }
}
