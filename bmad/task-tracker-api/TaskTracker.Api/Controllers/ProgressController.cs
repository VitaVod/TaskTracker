using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Progress.Contracts;
using TaskTracker.Api.Features.Progress.Repositories;
using TaskTracker.Api.Infrastructure.Authorization;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Authorize(Policy = AppPolicies.AuthenticatedUser)]
[Route("api/v1/progress")]
public class ProgressController(
    IProgressRepository progressRepository,
    ILogger<ProgressController> logger) : ControllerBase
{
    private const int MinWindowDays = 7;
    private const int MaxWindowDays = 90;
    private const int DefaultWindowDays = 30;

    [HttpGet("xp-summary")]
    [ProducesResponseType<ProgressXpSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetXpSummary(CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("progress.identity.invalid");
        }

        if (!await progressRepository.UserExistsAsync(userId, cancellationToken))
        {
            return NotFoundProblem("progress.user.not_found", "User account could not be found.");
        }

        var summary = await progressRepository.GetXpSummaryAsync(userId, cancellationToken);

        logger.LogInformation(
            "Progress XP summary served for user {UserId}. Entries: {Count}. TraceId: {TraceId}",
            userId,
            summary.LedgerEntryCount,
            HttpContext.TraceIdentifier);

        return Ok(new ProgressXpSummaryResponse(
            summary.TotalXp,
            summary.LedgerEntryCount,
            summary.LastGrantedAtUtc,
            new ProgressLevelSnapshotResponse(
                summary.LevelProgress.CurrentLevel,
                summary.LevelProgress.CurrentLevelThresholdXp,
                summary.LevelProgress.NextLevel,
                summary.LevelProgress.NextLevelThresholdXp,
                summary.LevelProgress.PercentToNextLevel,
                summary.LevelProgress.BandMilestoneLevels,
                summary.LevelProgress.ReachedBandCount,
                summary.LevelProgress.NextBandLevel),
            new ProgressExplanationResponse(summary.OutcomeReasonCode, summary.OutcomeExplanation)));
    }

    [HttpGet("streak")]
    [ProducesResponseType<ProgressStreakSnapshotResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStreak(CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("progress.identity.invalid");
        }

        if (!await progressRepository.UserExistsAsync(userId, cancellationToken))
        {
            return NotFoundProblem("progress.user.not_found", "User account could not be found.");
        }

        var snapshot = await progressRepository.GetStreakSnapshotAsync(userId, cancellationToken);

        logger.LogInformation(
            "Progress streak snapshot served for user {UserId}. Current: {CurrentStreak}. TraceId: {TraceId}",
            userId,
            snapshot.CurrentStreakDays,
            HttpContext.TraceIdentifier);

        return Ok(new ProgressStreakSnapshotResponse(
            snapshot.Outcome,
            snapshot.CurrentStreakDays,
            snapshot.LongestStreakDays,
            snapshot.TimeZoneId,
            snapshot.EvaluationWindowStartUtc,
            snapshot.EvaluationWindowEndUtc,
            snapshot.LastEvaluatedAtUtc,
            snapshot.IsRecoveryPromptVisible,
            snapshot.RecoveryReason,
            snapshot.RecommendedAction,
            new ProgressExplanationResponse(snapshot.OutcomeReasonCode, snapshot.OutcomeExplanation),
            snapshot.RecoveryExplanation is null
                ? null
                : new ProgressExplanationResponse(snapshot.RecoveryReason ?? "recovery-unavailable", snapshot.RecoveryExplanation)));
    }

    [HttpGet("trend")]
    [ProducesResponseType<ProgressTrendSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTrend([FromQuery] ProgressTrendQuery query, CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("progress.identity.invalid");
        }

        if (!await progressRepository.UserExistsAsync(userId, cancellationToken))
        {
            return NotFoundProblem("progress.user.not_found", "User account could not be found.");
        }

        if (!TryParseQuery(query, out var granularity, out var windowDays, out var errors))
        {
            return ValidationProblem("validation.request.invalid", errors);
        }

        var trend = await progressRepository.GetTrendSummaryAsync(
            userId,
            granularity,
            windowDays,
            DateTime.UtcNow,
            cancellationToken);

        logger.LogInformation(
            "Progress trend served for user {UserId}. Granularity: {Granularity}. WindowDays: {WindowDays}. TraceId: {TraceId}",
            userId,
            granularity,
            windowDays,
            HttpContext.TraceIdentifier);

        var response = new ProgressTrendSummaryResponse(
            granularity == ProgressTrendGranularity.Daily ? "daily" : "weekly",
            windowDays,
            trend.TimeZoneId,
            trend.RangeStartUtc,
            trend.RangeEndUtc,
            trend.Items.Select(item => new ProgressTrendPointResponse(
                item.BucketStartUtc,
                item.BucketEndUtc,
                item.CompletedTaskCount,
                item.XpGranted)).ToArray());

        return Ok(response);
    }

    private bool TryResolveCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(userIdClaim, out userId);
    }

    private static bool TryParseQuery(
        ProgressTrendQuery query,
        out ProgressTrendGranularity granularity,
        out int windowDays,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        granularity = ProgressTrendGranularity.Daily;
        if (!string.IsNullOrWhiteSpace(query.Granularity))
        {
            var normalizedGranularity = query.Granularity.Trim().ToLowerInvariant();
            if (normalizedGranularity == "daily")
            {
                granularity = ProgressTrendGranularity.Daily;
            }
            else if (normalizedGranularity == "weekly")
            {
                granularity = ProgressTrendGranularity.Weekly;
            }
            else
            {
                errors["granularity"] = ["The granularity field must be one of: daily, weekly."];
            }
        }

        windowDays = query.WindowDays ?? DefaultWindowDays;
        if (windowDays < MinWindowDays || windowDays > MaxWindowDays)
        {
            errors["windowDays"] = [$"The windowDays field must be between {MinWindowDays} and {MaxWindowDays}."];
        }

        return errors.Count == 0;
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
