using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Features.Auth.Email;
using TaskTracker.Api.Features.Auth.Repositories;
using TaskTracker.Api.Features.Auth.Tokens;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(
    IAuthRepository authRepository,
    IJwtTokenService tokenService,
    ITransactionalEmailService transactionalEmailService,
    IOptions<PasswordRecoveryOptions> passwordRecoveryOptions,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthController> logger) : ControllerBase
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly PasswordRecoveryOptions _passwordRecoveryOptions = passwordRecoveryOptions.Value;
    private const int PasswordRecoveryMaxDeliveryAttempts = 3;
    private static readonly TimeSpan PasswordRecoveryLifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Creates a new user account.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<RegisterResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authRepository.RegisterAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return ValidationProblem(result.Error ?? "Validation failed.");
        }

        return Created(string.Empty, new RegisterResponse(result.UserId, result.Email, "Account created successfully"));
    }

    /// <summary>
    /// Authenticates a user and issues access and refresh tokens backed by a server-side session.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authRepository.LoginAsync(request, cancellationToken);
        if (!result.IsSuccess)
        {
            return AuthenticationProblem();
        }

        var sessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var session = new RefreshSession
        {
            Id = sessionId,
            UserId = result.UserId,
            UserEmail = result.Email,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_jwtOptions.RefreshTokenDays),
            CreatedAtUtc = now
        };

        await authRepository.CreateSessionAsync(session, cancellationToken);

        var accessToken = tokenService.CreateAccessToken(result.UserId, result.Email, result.Role, sessionId);
        var refreshToken = tokenService.CreateRefreshToken(result.UserId, result.Email, result.Role, sessionId);

        logger.LogInformation("User {UserId} logged in; session {SessionId} created.", result.UserId, sessionId);

        return Ok(new LoginResponse(accessToken, refreshToken, tokenService.AccessTokenLifetimeInSeconds));
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new token pair, rotating the server-side session.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh")]
    [ProducesResponseType<RefreshResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        if (!tokenService.TryValidateRefreshToken(request.RefreshToken, out var oldSessionId, out var userId, out var email))
        {
            logger.LogWarning("Refresh attempt rejected: invalid or expired token.");
            return SessionInvalidProblem();
        }

        var newSessionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var newSession = new RefreshSession
        {
            Id = newSessionId,
            UserId = userId,
            UserEmail = email,
            IssuedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_jwtOptions.RefreshTokenDays),
            CreatedAtUtc = now
        };

        var rotateResult = await authRepository.RotateSessionAsync(oldSessionId, newSession, cancellationToken);

        switch (rotateResult.Outcome)
        {
            case RotateSessionOutcome.Success:
                break;

            case RotateSessionOutcome.ReplayDetected:
                logger.LogWarning(
                    "Replay attack detected for session {SessionId} by user {UserId}.",
                    oldSessionId, userId);
                return SessionInvalidProblem();

            default:
                logger.LogWarning(
                    "Refresh rejected for session {SessionId}: {Outcome}",
                    oldSessionId, rotateResult.Outcome);
                return SessionInvalidProblem();
        }

            var role = await authRepository.FindUserRoleAsync(userId, cancellationToken);
            if (string.IsNullOrWhiteSpace(role))
            {
                logger.LogWarning("Refresh rejected because user role is unavailable for user {UserId}.", userId);
                return SessionInvalidProblem();
            }

            var accessToken = tokenService.CreateAccessToken(userId, email, role, newSessionId);
            var refreshToken = tokenService.CreateRefreshToken(userId, email, role, newSessionId);

        return Ok(new RefreshResponse(accessToken, refreshToken, tokenService.AccessTokenLifetimeInSeconds));
    }

    /// <summary>
    /// Revokes the caller's active session so old tokens are rejected on protected endpoints.
    /// Idempotent: returns 200 even if the session was already revoked.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType<LogoutResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken cancellationToken)
    {
        // Prefer session_id from access token when present; fall back to refresh token.
        // This keeps logout revocation effective even after access-token expiry.
        var sessionIdClaim = User.FindFirstValue("session_id");

        if (sessionIdClaim is null || !Guid.TryParse(sessionIdClaim, out var sessionId))
        {
            // Fallback: parse the refresh token from the body to extract session id.
            if (!tokenService.TryValidateRefreshToken(request.RefreshToken, out sessionId, out _, out _))
            {
                // Still return 200 for idempotency — nothing to revoke.
                logger.LogWarning("Logout called with unresolvable session; returning 200 for idempotency.");
                return Ok(new LogoutResponse("Session revoked successfully"));
            }
        }

        await authRepository.RevokeSessionAsync(sessionId, "logout", cancellationToken);

        logger.LogInformation("Session {SessionId} revoked via logout.", sessionId);

        return Ok(new LogoutResponse("Session revoked successfully"));
    }

    [AllowAnonymous]
    [HttpPost("password-recovery/request")]
    [ProducesResponseType<PasswordRecoveryRequestResponse>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestPasswordRecovery(
        [FromBody] PasswordRecoveryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return ValidationProblem("Email is required.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        logger.LogInformation(
            "Password recovery request accepted for email domain flow. TraceId: {TraceId}",
            HttpContext.TraceIdentifier);

        var issued = await authRepository.IssuePasswordRecoveryTokenAsync(
            normalizedEmail,
            PasswordRecoveryLifetime,
            cancellationToken);

        if (issued is not null)
        {
            logger.LogInformation(
                "Password recovery token issued for user {UserId}. TokenId: {TokenId}. TraceId: {TraceId}",
                issued.UserId,
                issued.TokenId,
                HttpContext.TraceIdentifier);

            var recoveryLink = BuildRecoveryLink(issued.PlainTextToken);
            await DeliverRecoveryEmailWithRetryAsync(issued, recoveryLink, cancellationToken);
        }

        return Accepted(new PasswordRecoveryRequestResponse("If the account exists, a recovery email has been sent."));
    }

    [AllowAnonymous]
    [HttpPost("password-recovery/confirm")]
    [ProducesResponseType<PasswordRecoveryConfirmResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmPasswordRecovery(
        [FromBody] PasswordRecoveryConfirmRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return PasswordRecoveryInvalidProblem();
        }

        var result = await authRepository.ResetPasswordWithRecoveryTokenAsync(
            request.Token,
            request.NewPassword,
            cancellationToken);

        if (result.Outcome == PasswordResetWithRecoveryOutcome.Success)
        {
            logger.LogInformation("Password reset completed through recovery flow. TraceId: {TraceId}", HttpContext.TraceIdentifier);
            return Ok(new PasswordRecoveryConfirmResponse("Password updated successfully"));
        }

        if (result.Outcome == PasswordResetWithRecoveryOutcome.InvalidPassword)
        {
            return ValidationProblem(result.Error ?? "Password validation failed.");
        }

        logger.LogWarning(
            "Password recovery token rejected. Outcome: {Outcome}. TraceId: {TraceId}",
            result.Outcome,
            HttpContext.TraceIdentifier);

        return PasswordRecoveryInvalidProblem();
    }

    private async Task DeliverRecoveryEmailWithRetryAsync(
        PasswordRecoveryIssuanceResult issuance,
        string recoveryLink,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= PasswordRecoveryMaxDeliveryAttempts; attempt++)
        {
            logger.LogInformation(
                "Password recovery delivery attempt {Attempt}/{MaxAttempts}. TokenId: {TokenId}. TraceId: {TraceId}",
                attempt,
                PasswordRecoveryMaxDeliveryAttempts,
                issuance.TokenId,
                HttpContext.TraceIdentifier);

            var sendResult = await transactionalEmailService.SendPasswordRecoveryAsync(
                new PasswordRecoveryEmailMessage(issuance.TokenId, issuance.Email, recoveryLink, issuance.ExpiresAtUtc),
                cancellationToken);

            var success = sendResult == TransactionalEmailSendResult.Success;
            await authRepository.RecordPasswordRecoveryDeliveryAttemptAsync(
                issuance.TokenId,
                DateTime.UtcNow,
                success,
                cancellationToken);

            if (success)
            {
                logger.LogInformation(
                    "Password recovery delivery succeeded. TokenId: {TokenId}. Attempt={Attempt}. TraceId: {TraceId}",
                    issuance.TokenId,
                    attempt,
                    HttpContext.TraceIdentifier);
                return;
            }

            if (sendResult == TransactionalEmailSendResult.PermanentFailure)
            {
                logger.LogWarning(
                    "Password recovery delivery failed permanently. TokenId: {TokenId}. TraceId: {TraceId}",
                    issuance.TokenId,
                    HttpContext.TraceIdentifier);
                return;
            }
        }

        logger.LogError(
            "Password recovery delivery failed after retries. TokenId: {TokenId}. TraceId: {TraceId}",
            issuance.TokenId,
            HttpContext.TraceIdentifier);
    }

    private string BuildRecoveryLink(string rawToken)
    {
        var encodedToken = Uri.EscapeDataString(rawToken);
        var resetPath = string.IsNullOrWhiteSpace(_passwordRecoveryOptions.ResetPath)
            ? "/reset-password"
            : _passwordRecoveryOptions.ResetPath;

        if (Uri.TryCreate(_passwordRecoveryOptions.FrontendBaseUrl, UriKind.Absolute, out var baseUri))
        {
            var pathUri = Uri.TryCreate(resetPath, UriKind.RelativeOrAbsolute, out var parsedPath)
                ? parsedPath
                : new Uri("/reset-password", UriKind.Relative);

            var resetUri = pathUri!.IsAbsoluteUri ? pathUri : new Uri(baseUri, pathUri);
            var separator = string.IsNullOrEmpty(resetUri.Query) ? "?" : "&";
            return $"{resetUri}{separator}token={encodedToken}";
        }

        return $"{Request.Scheme}://{Request.Host}/reset-password?token={encodedToken}";
    }

    private ObjectResult ValidationProblem(string detail)
    {
        return Problem(
            type: "https://api.tasktracker.local/problems/validation-error",
            title: "Validation Error",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
    }

    private ObjectResult AuthenticationProblem()
    {
        return Problem(
            type: "https://api.tasktracker.local/problems/authentication-failed",
            title: "Authentication Failed",
            detail: "Invalid email or password",
            statusCode: StatusCodes.Status401Unauthorized);
    }

    private ObjectResult SessionInvalidProblem()
    {
        return Problem(
            type: "https://api.tasktracker.local/problems/session-invalid",
            title: "Session Invalid",
            detail: "The session is expired, revoked, or no longer valid.",
            statusCode: StatusCodes.Status401Unauthorized,
            extensions: new Dictionary<string, object?> { ["code"] = "auth.session.invalid" });
    }

    private ObjectResult PasswordRecoveryInvalidProblem()
    {
        return Problem(
            type: "https://api.tasktracker.local/problems/password-recovery-invalid",
            title: "Recovery Link Invalid",
            detail: "This recovery link is expired or already used. Request a new recovery email.",
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?> { ["code"] = "auth.password-recovery.invalid" });
    }
}
