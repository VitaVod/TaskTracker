using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TaskTracker.Api.Features.Leaderboards.Contracts;
using TaskTracker.Api.Features.Account.Repositories;
using TaskTracker.Api.Features.Leaderboards.Repositories;
using TaskTracker.Api.Features.Operations.Auditing;
using TaskTracker.Api.Features.Progress.Repositories;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api/v1/ops")]
public class OperationsController(
    IAccountRepository accountRepository,
    ILeaderboardRepository leaderboardRepository,
    IProgressRepository progressRepository,
    IPrivilegedAuditWriter privilegedAuditWriter,
    IAuthorizationService authorizationService,
    HealthCheckService healthCheckService,
    ILogger<OperationsController> logger,
    TaskTrackerDbContext dbContext) : ControllerBase
{
    private const int MinPage = 1;
    private const int DefaultPage = 1;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 50;
    private const int DefaultPageSize = 20;
    private const int MinWindowDays = 1;
    private const int MaxWindowDays = 90;
    private const int DefaultWindowDays = 14;
    private const int MinMarkerLimit = 1;
    private const int MaxMarkerLimit = 50;
    private const int DefaultMarkerLimit = 25;
    private const int MinTimelinePage = 1;
    private const int DefaultTimelinePage = 1;
    private const int MinTimelineMaxItems = 1;
    private const int MaxTimelineMaxItems = 100;
    private const int DefaultTimelineMaxItems = 50;
    private const int MaxTimelineWindowDays = 90;
    private const int MaxPrivilegedAuditWindowDays = 90;
    private const int MinPrivilegedAuditPage = 1;
    private const int DefaultPrivilegedAuditPage = 1;
    private const int MinPrivilegedAuditPageSize = 1;
    private const int MaxPrivilegedAuditPageSize = 100;
    private const int DefaultPrivilegedAuditPageSize = 25;
    private const int MinIntegrationFailurePage = 1;
    private const int DefaultIntegrationFailurePage = 1;
    private const int MinIntegrationFailurePageSize = 1;
    private const int MaxIntegrationFailurePageSize = 100;
    private const int DefaultIntegrationFailurePageSize = 25;
    private const int MaxIntegrationFailureWindowDays = 90;

    private static readonly Meter SuspiciousCaseMeter = new("TaskTracker.Api.Operations", "1.0.0");
    private static readonly Counter<long> SuspiciousCaseQueryCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.suspicious_cases.query.total");
    private static readonly Counter<long> SuspiciousCaseEmptyCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.suspicious_cases.query.empty_total");
    private static readonly Counter<long> SuspiciousCaseForbiddenCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.suspicious_cases.query.forbidden_total");
    private static readonly Counter<long> SupportDiagnosticsSuccessCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.support_diagnostics.query.total");
    private static readonly Counter<long> SupportDiagnosticsEmptyCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.support_diagnostics.query.empty_total");
    private static readonly Counter<long> SupportDiagnosticsForbiddenCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.support_diagnostics.query.forbidden_total");
    private static readonly Counter<long> SupportTimelineSuccessCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.support_timeline.query.total");
    private static readonly Counter<long> SupportTimelineEmptyCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.support_timeline.query.empty_total");
    private static readonly Counter<long> SupportTimelineForbiddenCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.support_timeline.query.forbidden_total");
    private static readonly Counter<long> SupportTimelineInvalidFilterCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.support_timeline.query.invalid_filter_total");
    private static readonly Histogram<double> SupportTimelineLatencyMs =
        SuspiciousCaseMeter.CreateHistogram<double>("ops.support_timeline.query.latency_ms");
    private static readonly Counter<long> ModerationAttemptedCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.suspicious_cases.moderation.attempted_total");
    private static readonly Counter<long> ModerationSucceededCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.suspicious_cases.moderation.succeeded_total");
    private static readonly Counter<long> ModerationRejectedCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.suspicious_cases.moderation.rejected_total");
    private static readonly Counter<long> ModerationFailedCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.suspicious_cases.moderation.failed_total");
    private static readonly Counter<long> PrivilegedAuditWriteAttemptedCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.privileged_audit.write.attempted_total");
    private static readonly Counter<long> PrivilegedAuditWriteSucceededCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.privileged_audit.write.succeeded_total");
    private static readonly Counter<long> PrivilegedAuditWriteRejectedCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.privileged_audit.write.rejected_total");
    private static readonly Counter<long> PrivilegedAuditWriteFailedCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.privileged_audit.write.failed_total");
    private static readonly Counter<long> PrivilegedAuditQueryCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.privileged_audit.query.total");
    private static readonly Counter<long> PrivilegedAuditQueryForbiddenCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.privileged_audit.query.forbidden_total");
    private static readonly Counter<long> PrivilegedAuditQueryValidationCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.privileged_audit.query.invalid_filter_total");
    private static readonly Histogram<double> PrivilegedAuditQueryLatencyMs =
        SuspiciousCaseMeter.CreateHistogram<double>("ops.privileged_audit.query.latency_ms");
    private static readonly Counter<long> IntegrationFailureQueryCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.integration_failures.query.total");
    private static readonly Counter<long> IntegrationFailureQueryForbiddenCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.integration_failures.query.forbidden_total");
    private static readonly Counter<long> IntegrationFailureQueryValidationCounter =
        SuspiciousCaseMeter.CreateCounter<long>("ops.integration_failures.query.invalid_filter_total");
    private static readonly Histogram<double> IntegrationFailureQueryLatencyMs =
        SuspiciousCaseMeter.CreateHistogram<double>("ops.integration_failures.query.latency_ms");

    private const string RankingCorrectionActionType = "rankingCorrection";
    private const string FlagEntityActionType = "flagEntity";
    private const string PrivilegedModerationActionType = "moderation.apply";
    private const int ReasonCodeMaxLength = 64;
    private const int ReasonTextMaxLength = 512;

    private static readonly TimeSpan ConfirmationWindow = TimeSpan.FromMinutes(10);
    private static readonly byte[] ConfirmationSecret = Encoding.UTF8.GetBytes("TaskTracker::Ops::Moderation::v1");
    private static readonly string[] SupportedTimelineEventTypes = ["taskCompletion", "xpLedger", "moderation", "streakEvaluation"];

    [HttpGet("admin/health")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminHealth()
    {
        var emailHealthReport = await healthCheckService.CheckHealthAsync(
            registration => string.Equals(registration.Name, "email_configuration", StringComparison.Ordinal),
            HttpContext.RequestAborted);

        var emailConfiguration = new
        {
            status = emailHealthReport.Status.ToString().ToLowerInvariant(),
            details = emailHealthReport.Entries.TryGetValue("email_configuration", out var entry)
                ? entry.Data.ToDictionary(pair => pair.Key, pair => pair.Value ?? string.Empty)
                : new Dictionary<string, object>()
        };

        var status = emailHealthReport.Status == HealthStatus.Healthy ? "ok" : "degraded";

        return Ok(new
        {
            status,
            capability = "admin",
            emailConfiguration,
            traceId = HttpContext.TraceIdentifier
        });
    }

    [HttpGet("support/users/{userId:guid}")]
    [Authorize(Policy = AppPolicies.AuthenticatedUser)]
    [ProducesResponseType<SupportUserDiagnosticResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupportUserSnapshot(
        [FromRoute] Guid userId,
        [FromQuery] SupportDiagnosticQuery query,
        CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role) ?? "unknown";

        var authorization = await authorizationService.AuthorizeAsync(User, resource: null, policyName: AppPolicies.SupportOnly);
        if (!authorization.Succeeded)
        {
            SupportDiagnosticsForbiddenCounter.Add(1);
            logger.LogWarning(
                "Support diagnostics denied. ActorId: {ActorId}. ActorRole: {ActorRole}. TargetUserId: {TargetUserId}. TraceId: {TraceId}",
                actorId,
                actorRole,
                userId,
                HttpContext.TraceIdentifier);

            return ForbiddenProblem();
        }

        if (!TryParseSupportDiagnosticQuery(query, out var windowDays, out var markerLimit, out var validationErrors))
        {
            return ValidationProblem("validation.request.invalid", validationErrors);
        }

        var user = await accountRepository.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return NotFoundProblem("account.user.not_found", "User account could not be found.");
        }

        var xpSummary = await progressRepository.GetXpSummaryAsync(userId, cancellationToken);
        var streakSnapshot = await progressRepository.GetStreakSnapshotAsync(userId, cancellationToken);

        var recentCompletions = await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.UserId == userId && task.IsCompleted)
            .OrderByDescending(task => task.UpdatedAtUtc)
            .ThenBy(task => task.Id)
            .Take(markerLimit)
            .Select(task => new SupportRecentCompletion(
                task.Id,
                task.Title,
                task.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        var taskSummary = await dbContext.Tasks
            .AsNoTracking()
            .Where(task => task.UserId == userId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalCount = group.Count(),
                CompletedCount = group.Count(task => task.IsCompleted),
                LastCompletedAtUtc = group
                    .Where(task => task.IsCompleted)
                    .Max(task => (DateTime?)task.UpdatedAtUtc)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var windowStartUtc = DateTime.UtcNow.AddDays(-windowDays);

        var completionMarkers = await dbContext.TaskCompletionEvents
            .AsNoTracking()
            .Where(completionEvent => completionEvent.OwnerId == userId
                && completionEvent.OccurredAtUtc >= windowStartUtc)
            .OrderByDescending(completionEvent => completionEvent.OccurredAtUtc)
            .ThenBy(completionEvent => completionEvent.Id)
            .Take(markerLimit)
            .Select(completionEvent => new SupportProgressMarker(
                "taskCompletionEvent",
                completionEvent.Id.ToString(),
                completionEvent.OccurredAtUtc,
                $"{completionEvent.EventName} for task {completionEvent.TaskId:N}",
                null,
                completionEvent.IdempotencyKey))
            .ToListAsync(cancellationToken);

        var xpMarkers = await dbContext.XpLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == userId
                && entry.OccurredAtUtc >= windowStartUtc)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenBy(entry => entry.Id)
            .Take(markerLimit)
            .Select(entry => new SupportProgressMarker(
                "xpLedgerEntry",
                entry.Id.ToString(),
                entry.OccurredAtUtc,
                $"{entry.EventName}: {entry.XpGranted} XP",
                null,
                entry.IdempotencyKey))
            .ToListAsync(cancellationToken);

        var privilegedAuditMarkers = await dbContext.PrivilegedActionAudits
            .AsNoTracking()
            .Where(audit => audit.TargetUserId == userId
                && audit.OccurredAtUtc >= windowStartUtc)
            .OrderByDescending(audit => audit.OccurredAtUtc)
            .ThenBy(audit => audit.Id)
            .Take(markerLimit)
            .Select(audit => new SupportProgressMarker(
                "privilegedAudit",
                audit.Id.ToString(),
                audit.OccurredAtUtc,
                $"{audit.ActionType} ({audit.Outcome})",
                audit.TraceId,
                audit.CorrelationId))
            .ToListAsync(cancellationToken);

        var recentMarkers = completionMarkers
            .Concat(xpMarkers)
            .Concat(privilegedAuditMarkers)
            .OrderByDescending(marker => marker.OccurredAtUtc)
            .ThenBy(marker => marker.MarkerType)
            .ThenBy(marker => marker.MarkerId)
            .Take(markerLimit)
            .ToArray();

        if (recentMarkers.Length == 0)
        {
            SupportDiagnosticsEmptyCounter.Add(1);
        }

        SupportDiagnosticsSuccessCounter.Add(1);

        var correlationId = ResolveCorrelationId();

        logger.LogInformation(
            "Support diagnostics served. ActorId: {ActorId}. ActorRole: {ActorRole}. TargetUserId: {TargetUserId}. WindowDays: {WindowDays}. MarkerLimit: {MarkerLimit}. MarkerCount: {MarkerCount}. CorrelationId: {CorrelationId}. TraceId: {TraceId}",
            actorId,
            actorRole,
            userId,
            windowDays,
            markerLimit,
            recentMarkers.Length,
            correlationId,
            HttpContext.TraceIdentifier);

        return Ok(new SupportUserDiagnosticResponse(
            new SupportAccountSnapshot(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role,
                user.TimeZoneId,
                user.Locale,
                user.LeaderboardParticipationMode == LeaderboardParticipationMode.Public
                    ? "public"
                    : user.LeaderboardParticipationMode == LeaderboardParticipationMode.Anonymous
                        ? "anonymous"
                        : "hidden",
                user.IsSuspiciousFlagged,
                user.CreatedAtUtc,
                user.ModifiedAtUtc),
            new SupportTaskStateSnapshot(
                taskSummary?.TotalCount ?? 0,
                taskSummary?.CompletedCount ?? 0,
                Math.Max(0, (taskSummary?.TotalCount ?? 0) - (taskSummary?.CompletedCount ?? 0)),
                taskSummary?.LastCompletedAtUtc,
                recentCompletions.ToArray()),
            new SupportXpStateSnapshot(
                xpSummary.TotalXp,
                xpSummary.LedgerEntryCount,
                xpSummary.LastGrantedAtUtc,
                xpSummary.OutcomeReasonCode,
                xpSummary.OutcomeExplanation),
            new SupportStreakStateSnapshot(
                streakSnapshot.Outcome.ToString(),
                streakSnapshot.CurrentStreakDays,
                streakSnapshot.LongestStreakDays,
                streakSnapshot.TimeZoneId,
                streakSnapshot.EvaluationWindowStartUtc,
                streakSnapshot.EvaluationWindowEndUtc,
                streakSnapshot.LastEvaluatedAtUtc,
                streakSnapshot.OutcomeReasonCode,
                streakSnapshot.OutcomeExplanation,
                streakSnapshot.IsRecoveryPromptVisible,
                streakSnapshot.RecoveryReason,
                streakSnapshot.RecommendedAction,
                streakSnapshot.RecoveryExplanation),
            new SupportDiagnosticWindow(
                windowDays,
                windowStartUtc,
                markerLimit),
            recentMarkers,
            correlationId,
            HttpContext.TraceIdentifier));
    }

    [HttpGet("support/users/{userId:guid}/timeline")]
    [Authorize(Policy = AppPolicies.AuthenticatedUser)]
    [ProducesResponseType<SupportTimelineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSupportUserTimeline(
        [FromRoute] Guid userId,
        [FromQuery] SupportTimelineQuery query,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role) ?? "unknown";

        var authorization = await authorizationService.AuthorizeAsync(User, resource: null, policyName: AppPolicies.SupportOnly);
        if (!authorization.Succeeded)
        {
            SupportTimelineForbiddenCounter.Add(1);
            logger.LogWarning(
                "Support timeline denied. ActorId: {ActorId}. ActorRole: {ActorRole}. TargetUserId: {TargetUserId}. TraceId: {TraceId}",
                actorId,
                actorRole,
                userId,
                HttpContext.TraceIdentifier);

            return ForbiddenProblem();
        }

        if (!TryParseSupportTimelineQuery(query, out var parsedQuery, out var validationErrors))
        {
            SupportTimelineInvalidFilterCounter.Add(1);
            return ValidationProblem("validation.request.invalid", validationErrors);
        }

        var userExists = await accountRepository.FindUserByIdAsync(userId, cancellationToken);
        if (userExists is null)
        {
            return NotFoundProblem("account.user.not_found", "User account could not be found.");
        }

        var completionEvents = await dbContext.TaskCompletionEvents
            .AsNoTracking()
            .Where(completionEvent => completionEvent.OwnerId == userId
                && completionEvent.OccurredAtUtc >= parsedQuery.StartUtc
                && completionEvent.OccurredAtUtc <= parsedQuery.EndUtc)
            .Select(completionEvent => new SupportTimelineEvent(
                completionEvent.Id.ToString(),
                "taskCompletion",
                completionEvent.OccurredAtUtc,
                "progression",
                completionEvent.ResultingIsCompleted ? "task.completion.recorded" : "task.reopened.recorded",
                completionEvent.ResultingIsCompleted
                    ? $"Task completion event recorded for task {completionEvent.TaskId:N}."
                    : $"Task reopened event recorded for task {completionEvent.TaskId:N}.",
                completionEvent.ResultingIsCompleted ? "completed" : "active",
                null,
                completionEvent.IdempotencyKey,
                "system",
                $"user:{completionEvent.OwnerId:N}",
                completionEvent.TaskId.ToString()))
            .ToListAsync(cancellationToken);

        var xpEvents = await dbContext.XpLedgerEntries
            .AsNoTracking()
            .Where(entry => entry.OwnerId == userId
                && entry.OccurredAtUtc >= parsedQuery.StartUtc
                && entry.OccurredAtUtc <= parsedQuery.EndUtc)
            .Select(entry => new SupportTimelineEvent(
                entry.Id.ToString(),
                "xpLedger",
                entry.OccurredAtUtc,
                "progression",
                "progress.xp.recorded",
                $"{entry.EventName} granted {entry.XpGranted} XP for task {entry.TaskId:N}.",
                $"xpGranted:{entry.XpGranted}",
                null,
                entry.IdempotencyKey,
                "system",
                $"user:{entry.OwnerId:N}",
                entry.TaskId.ToString()))
            .ToListAsync(cancellationToken);

        var privilegedAuditEvents = await dbContext.PrivilegedActionAudits
            .AsNoTracking()
            .Where(audit => audit.TargetUserId == userId
                && audit.OccurredAtUtc >= parsedQuery.StartUtc
                && audit.OccurredAtUtc <= parsedQuery.EndUtc)
            .Select(audit => new SupportTimelineEvent(
                audit.Id.ToString(),
                "moderation",
                audit.OccurredAtUtc,
                "moderation",
                $"moderation.{audit.ActionType}.audit",
                $"Privileged action {audit.ActionType} completed with outcome {audit.Outcome}.",
                audit.Outcome,
                audit.TraceId,
                audit.CorrelationId,
                $"{audit.ActorRole}:{audit.ActorUserId}",
                audit.TargetUserId.HasValue ? $"user:{audit.TargetUserId.Value:N}" : "user:unknown",
                null))
            .ToListAsync(cancellationToken);

        var streakEvents = await dbContext.UserStreakSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.OwnerId == userId
                && snapshot.LastEvaluatedAtUtc >= parsedQuery.StartUtc
                && snapshot.LastEvaluatedAtUtc <= parsedQuery.EndUtc)
            .Select(snapshot => new SupportTimelineEvent(
                snapshot.LastEvaluatedEventId.ToString(),
                "streakEvaluation",
                snapshot.LastEvaluatedAtUtc,
                "streak",
                "progress.streak.evaluated",
                $"Streak evaluated with outcome {snapshot.Outcome} ({snapshot.CurrentStreakDays} current / {snapshot.LongestStreakDays} longest).",
                snapshot.Outcome.ToString(),
                snapshot.LastEvaluationTraceId,
                snapshot.LastEvaluationTraceId,
                "system",
                $"user:{snapshot.OwnerId:N}",
                snapshot.LastEvaluatedEventId.ToString()))
            .ToListAsync(cancellationToken);

        var merged = completionEvents
            .Concat(xpEvents)
            .Concat(privilegedAuditEvents)
            .Concat(streakEvents);

        if (!string.IsNullOrWhiteSpace(parsedQuery.EventType))
        {
            merged = merged.Where(item => string.Equals(item.EventType, parsedQuery.EventType, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = merged
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenBy(item => item.EventType)
            .ThenBy(item => item.EventId)
            .ToList();

        var totalCount = ordered.Count;
        var skip = (parsedQuery.Page - 1) * parsedQuery.MaxItems;
        var pagedItems = ordered
            .Skip(skip)
            .Take(parsedQuery.MaxItems)
            .ToArray();

        if (totalCount == 0)
        {
            SupportTimelineEmptyCounter.Add(1);
        }

        SupportTimelineSuccessCounter.Add(1);
        SupportTimelineLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

        var correlationId = ResolveCorrelationId();

        logger.LogInformation(
            "Support timeline served. ActorId: {ActorId}. ActorRole: {ActorRole}. TargetUserId: {TargetUserId}. EventType: {EventType}. StartUtc: {StartUtc}. EndUtc: {EndUtc}. Page: {Page}. MaxItems: {MaxItems}. Returned: {Returned}. Total: {Total}. CorrelationId: {CorrelationId}. TraceId: {TraceId}",
            actorId,
            actorRole,
            userId,
            parsedQuery.EventType ?? "all",
            parsedQuery.StartUtc,
            parsedQuery.EndUtc,
            parsedQuery.Page,
            parsedQuery.MaxItems,
            pagedItems.Length,
            totalCount,
            correlationId,
            HttpContext.TraceIdentifier);

        return Ok(new SupportTimelineResponse(
            parsedQuery.Page,
            parsedQuery.MaxItems,
            totalCount,
            skip + pagedItems.Length < totalCount,
            new SupportTimelineFilterEnvelope(
                parsedQuery.EventType,
                parsedQuery.StartUtc,
                parsedQuery.EndUtc),
            pagedItems,
            correlationId,
            HttpContext.TraceIdentifier));
    }

    [HttpGet("admin-support/privileged-audits")]
    [Authorize(Policy = AppPolicies.AuthenticatedUser)]
    [ProducesResponseType<PrivilegedAuditQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPrivilegedAudits(
        [FromQuery] PrivilegedAuditQuery query,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role) ?? "unknown";
        PrivilegedAuditQueryCounter.Add(1);

        if (!IsAdminOrSupport(actorRole))
        {
            PrivilegedAuditQueryForbiddenCounter.Add(1);
            logger.LogWarning(
                "Privileged audit query denied. ActorId: {ActorId}. ActorRole: {ActorRole}. TraceId: {TraceId}",
                actorId,
                actorRole,
                HttpContext.TraceIdentifier);
            return ForbiddenProblem();
        }

        if (!TryParsePrivilegedAuditQuery(query, out var parsedQuery, out var validationErrors))
        {
            PrivilegedAuditQueryValidationCounter.Add(1);
            return ValidationProblem("validation.request.invalid", validationErrors);
        }

        var audits = dbContext.PrivilegedActionAudits.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(parsedQuery.ActorUserId))
        {
            audits = audits.Where(item => item.ActorUserId == parsedQuery.ActorUserId);
        }

        if (parsedQuery.TargetUserId.HasValue)
        {
            audits = audits.Where(item => item.TargetUserId == parsedQuery.TargetUserId);
        }

        if (!string.IsNullOrWhiteSpace(parsedQuery.ActionType))
        {
            audits = audits.Where(item => item.ActionType == parsedQuery.ActionType);
        }

        audits = audits.Where(item => item.OccurredAtUtc >= parsedQuery.StartUtc && item.OccurredAtUtc <= parsedQuery.EndUtc);

        var totalCount = await audits.CountAsync(cancellationToken);
        var skip = (parsedQuery.Page - 1) * parsedQuery.PageSize;
        var items = await audits
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .Skip(skip)
            .Take(parsedQuery.PageSize)
            .Select(item => new PrivilegedAuditItem(
                item.Id,
                item.ActorUserId,
                item.ActorRole,
                item.TargetUserId,
                item.ActionType,
                item.ReasonCode,
                item.ReasonText,
                item.Outcome,
                item.OccurredAtUtc,
                item.CorrelationId,
                item.TraceId))
            .ToArrayAsync(cancellationToken);

        PrivilegedAuditQueryLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

        var correlationId = ResolveCorrelationId();
        logger.LogInformation(
            "Privileged audit query served. ActorId: {ActorId}. ActorRole: {ActorRole}. ActorFilter: {ActorFilter}. TargetFilter: {TargetFilter}. ActionType: {ActionType}. StartUtc: {StartUtc}. EndUtc: {EndUtc}. Page: {Page}. PageSize: {PageSize}. Returned: {Returned}. Total: {Total}. CorrelationId: {CorrelationId}. TraceId: {TraceId}",
            actorId,
            actorRole,
            parsedQuery.ActorUserId ?? "all",
            parsedQuery.TargetUserId?.ToString("N") ?? "all",
            parsedQuery.ActionType ?? "all",
            parsedQuery.StartUtc,
            parsedQuery.EndUtc,
            parsedQuery.Page,
            parsedQuery.PageSize,
            items.Length,
            totalCount,
            correlationId,
            HttpContext.TraceIdentifier);

        return Ok(new PrivilegedAuditQueryResponse(
            parsedQuery.Page,
            parsedQuery.PageSize,
            totalCount,
            skip + items.Length < totalCount,
            new PrivilegedAuditFilterEnvelope(
                parsedQuery.ActorUserId,
                parsedQuery.TargetUserId,
                parsedQuery.ActionType,
                parsedQuery.StartUtc,
                parsedQuery.EndUtc),
            items,
            correlationId,
            HttpContext.TraceIdentifier));
    }

    [HttpGet("admin-support/integration-failures")]
    [Authorize(Policy = AppPolicies.AuthenticatedUser)]
    [ProducesResponseType<IntegrationFailureQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetIntegrationFailures(
        [FromQuery] IntegrationFailureQuery query,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role) ?? "unknown";

        IntegrationFailureQueryCounter.Add(1);

        if (!IsAdminOrSupport(actorRole))
        {
            IntegrationFailureQueryForbiddenCounter.Add(1);
            logger.LogWarning(
                "Integration failure query denied. ActorId: {ActorId}. ActorRole: {ActorRole}. TraceId: {TraceId}",
                actorId,
                actorRole,
                HttpContext.TraceIdentifier);
            return ForbiddenProblem();
        }

        if (!TryParseIntegrationFailureQuery(query, out var parsedQuery, out var validationErrors))
        {
            IntegrationFailureQueryValidationCounter.Add(1);
            return ValidationProblem("validation.request.invalid", validationErrors);
        }

        var failureEvents = dbContext.IntegrationProcessingFailureEvents.AsNoTracking().AsQueryable();
        failureEvents = failureEvents.Where(item => item.OccurredAtUtc >= parsedQuery.StartUtc && item.OccurredAtUtc <= parsedQuery.EndUtc);

        if (!string.IsNullOrWhiteSpace(parsedQuery.IntegrationId))
        {
            failureEvents = failureEvents.Where(item => item.IntegrationId == parsedQuery.IntegrationId);
        }

        if (parsedQuery.OwnerUserId.HasValue)
        {
            failureEvents = failureEvents.Where(item => item.OwnerUserId == parsedQuery.OwnerUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(parsedQuery.ErrorClass))
        {
            failureEvents = failureEvents.Where(item => item.ErrorClass == parsedQuery.ErrorClass);
        }

        if (!string.IsNullOrWhiteSpace(parsedQuery.CorrelationId))
        {
            failureEvents = failureEvents.Where(item => item.CorrelationId == parsedQuery.CorrelationId);
        }

        if (!string.IsNullOrWhiteSpace(parsedQuery.TraceId))
        {
            failureEvents = failureEvents.Where(item => item.TraceId == parsedQuery.TraceId);
        }

        var totalCount = await failureEvents.CountAsync(cancellationToken);
        var skip = (parsedQuery.Page - 1) * parsedQuery.PageSize;
        var items = await failureEvents
            .OrderByDescending(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .Skip(skip)
            .Take(parsedQuery.PageSize)
            .Select(item => new IntegrationFailureItem(
                item.Id,
                item.OccurredAtUtc,
                item.IntegrationId,
                item.OwnerUserId,
                item.ExternalTaskId,
                item.IdempotencyKey,
                item.ErrorClass,
                item.ErrorCode,
                item.HttpStatus,
                item.CorrelationId,
                item.TraceId))
            .ToArrayAsync(cancellationToken);

        IntegrationFailureQueryLatencyMs.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

        var correlationId = ResolveCorrelationId();
        logger.LogInformation(
            "Integration failure query served. ActorId: {ActorId}. ActorRole: {ActorRole}. IntegrationId: {IntegrationId}. OwnerUserId: {OwnerUserId}. ErrorClass: {ErrorClass}. StartUtc: {StartUtc}. EndUtc: {EndUtc}. Page: {Page}. PageSize: {PageSize}. Returned: {Returned}. Total: {Total}. CorrelationId: {CorrelationId}. TraceId: {TraceId}",
            actorId,
            actorRole,
            parsedQuery.IntegrationId ?? "all",
            parsedQuery.OwnerUserId?.ToString("N") ?? "all",
            parsedQuery.ErrorClass ?? "all",
            parsedQuery.StartUtc,
            parsedQuery.EndUtc,
            parsedQuery.Page,
            parsedQuery.PageSize,
            items.Length,
            totalCount,
            correlationId,
            HttpContext.TraceIdentifier);

        return Ok(new IntegrationFailureQueryResponse(
            parsedQuery.Page,
            parsedQuery.PageSize,
            totalCount,
            skip + items.Length < totalCount,
            new IntegrationFailureFilterEnvelope(
                parsedQuery.IntegrationId,
                parsedQuery.OwnerUserId,
                parsedQuery.ErrorClass,
                parsedQuery.CorrelationId,
                parsedQuery.TraceId,
                parsedQuery.StartUtc,
                parsedQuery.EndUtc),
            items,
            correlationId,
            HttpContext.TraceIdentifier));
    }

    private static bool TryParseSupportDiagnosticQuery(
        SupportDiagnosticQuery query,
        out int windowDays,
        out int markerLimit,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        windowDays = query.WindowDays ?? DefaultWindowDays;
        if (windowDays < MinWindowDays || windowDays > MaxWindowDays)
        {
            errors["windowDays"] = [$"The windowDays field must be between {MinWindowDays} and {MaxWindowDays}."];
        }

        markerLimit = query.MarkerLimit ?? DefaultMarkerLimit;
        if (markerLimit < MinMarkerLimit || markerLimit > MaxMarkerLimit)
        {
            errors["markerLimit"] = [$"The markerLimit field must be between {MinMarkerLimit} and {MaxMarkerLimit}."];
        }

        return errors.Count == 0;
    }

    private static bool TryParseSupportTimelineQuery(
        SupportTimelineQuery query,
        out ParsedSupportTimelineQuery parsedQuery,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var eventType = string.IsNullOrWhiteSpace(query.EventType)
            ? null
            : query.EventType.Trim();

        if (!string.IsNullOrWhiteSpace(eventType)
            && !SupportedTimelineEventTypes.Any(supported => string.Equals(supported, eventType, StringComparison.OrdinalIgnoreCase)))
        {
            errors["eventType"] = [$"The eventType field must be one of: {string.Join(", ", SupportedTimelineEventTypes)}."];
        }

        var page = query.Page ?? DefaultTimelinePage;
        if (page < MinTimelinePage)
        {
            errors["page"] = [$"The page field must be greater than or equal to {MinTimelinePage}."];
        }

        var maxItems = query.MaxItems ?? DefaultTimelineMaxItems;
        if (maxItems < MinTimelineMaxItems || maxItems > MaxTimelineMaxItems)
        {
            errors["maxItems"] = [$"The maxItems field must be between {MinTimelineMaxItems} and {MaxTimelineMaxItems}."];
        }

        var endUtc = query.EndUtc ?? DateTime.UtcNow;
        var startUtc = query.StartUtc ?? endUtc.AddDays(-DefaultWindowDays);

        if (startUtc > endUtc)
        {
            errors["dateRange"] = ["The startUtc field must be less than or equal to endUtc."];
        }
        else if ((endUtc - startUtc).TotalDays > MaxTimelineWindowDays)
        {
            errors["dateRange"] = [$"The date range cannot exceed {MaxTimelineWindowDays} days."];
        }

        parsedQuery = new ParsedSupportTimelineQuery(
            eventType,
            startUtc,
            endUtc,
            page,
            maxItems);

        return errors.Count == 0;
    }

    private static bool TryParsePrivilegedAuditQuery(
        PrivilegedAuditQuery query,
        out ParsedPrivilegedAuditQuery parsedQuery,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var actorUserId = string.IsNullOrWhiteSpace(query.ActorUserId)
            ? null
            : query.ActorUserId.Trim();

        var actionType = string.IsNullOrWhiteSpace(query.ActionType)
            ? null
            : query.ActionType.Trim();

        Guid? targetUserId = null;
        if (!string.IsNullOrWhiteSpace(query.TargetUserId))
        {
            if (Guid.TryParse(query.TargetUserId.Trim(), out var parsedTargetUserId))
            {
                targetUserId = parsedTargetUserId;
            }
            else
            {
                errors["targetUserId"] = ["The targetUserId field must be a valid GUID."];
            }
        }

        var page = query.Page ?? DefaultPrivilegedAuditPage;
        if (page < MinPrivilegedAuditPage)
        {
            errors["page"] = [$"The page field must be greater than or equal to {MinPrivilegedAuditPage}."];
        }

        var pageSize = query.PageSize ?? DefaultPrivilegedAuditPageSize;
        if (pageSize < MinPrivilegedAuditPageSize || pageSize > MaxPrivilegedAuditPageSize)
        {
            errors["pageSize"] = [$"The pageSize field must be between {MinPrivilegedAuditPageSize} and {MaxPrivilegedAuditPageSize}."];
        }

        var endUtc = query.EndUtc ?? DateTime.UtcNow;
        var startUtc = query.StartUtc ?? endUtc.AddDays(-DefaultWindowDays);

        if (startUtc > endUtc)
        {
            errors["dateRange"] = ["The startUtc field must be less than or equal to endUtc."];
        }
        else if ((endUtc - startUtc).TotalDays > MaxPrivilegedAuditWindowDays)
        {
            errors["dateRange"] = [$"The date range cannot exceed {MaxPrivilegedAuditWindowDays} days."];
        }

        parsedQuery = new ParsedPrivilegedAuditQuery(
            actorUserId,
            targetUserId,
            actionType,
            startUtc,
            endUtc,
            page,
            pageSize);

        return errors.Count == 0;
    }

    private static bool TryParseIntegrationFailureQuery(
        IntegrationFailureQuery query,
        out ParsedIntegrationFailureQuery parsedQuery,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        var integrationId = string.IsNullOrWhiteSpace(query.IntegrationId)
            ? null
            : query.IntegrationId.Trim().ToLowerInvariant();

        var errorClass = string.IsNullOrWhiteSpace(query.ErrorClass)
            ? null
            : query.ErrorClass.Trim().ToLowerInvariant();

        var correlationId = string.IsNullOrWhiteSpace(query.CorrelationId)
            ? null
            : query.CorrelationId.Trim();

        var traceId = string.IsNullOrWhiteSpace(query.TraceId)
            ? null
            : query.TraceId.Trim();

        Guid? ownerUserId = null;
        if (!string.IsNullOrWhiteSpace(query.OwnerUserId))
        {
            if (Guid.TryParse(query.OwnerUserId.Trim(), out var parsedOwnerUserId))
            {
                ownerUserId = parsedOwnerUserId;
            }
            else
            {
                errors["ownerUserId"] = ["The ownerUserId field must be a valid GUID."];
            }
        }

        var page = query.Page ?? DefaultIntegrationFailurePage;
        if (page < MinIntegrationFailurePage)
        {
            errors["page"] = [$"The page field must be greater than or equal to {MinIntegrationFailurePage}."];
        }

        var pageSize = query.PageSize ?? DefaultIntegrationFailurePageSize;
        if (pageSize < MinIntegrationFailurePageSize || pageSize > MaxIntegrationFailurePageSize)
        {
            errors["pageSize"] = [$"The pageSize field must be between {MinIntegrationFailurePageSize} and {MaxIntegrationFailurePageSize}."];
        }

        var endUtc = query.EndUtc ?? DateTime.UtcNow;
        var startUtc = query.StartUtc ?? endUtc.AddDays(-DefaultWindowDays);

        if (startUtc > endUtc)
        {
            errors["dateRange"] = ["The startUtc field must be less than or equal to endUtc."];
        }
        else if ((endUtc - startUtc).TotalDays > MaxIntegrationFailureWindowDays)
        {
            errors["dateRange"] = [$"The date range cannot exceed {MaxIntegrationFailureWindowDays} days."];
        }

        parsedQuery = new ParsedIntegrationFailureQuery(
            integrationId,
            ownerUserId,
            errorClass,
            correlationId,
            traceId,
            startUtc,
            endUtc,
            page,
            pageSize);

        return errors.Count == 0;
    }

    private static bool IsAdminOrSupport(string actorRole)
    {
        return string.Equals(actorRole, AppRoles.Admin, StringComparison.Ordinal)
            || string.Equals(actorRole, AppRoles.Support, StringComparison.Ordinal);
    }

    private string ResolveCorrelationId(string? fallbackCorrelationId = null)
    {
        var correlationId = HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(fallbackCorrelationId))
        {
            return fallbackCorrelationId.Trim();
        }

        return HttpContext.TraceIdentifier;
    }

    private static string ComputeAuditIntentKey(
        string actorId,
        string resourceKey,
        string actionType,
        string outcome,
        string? reasonCode,
        string? reasonText)
    {
        var normalized = string.Join('|',
            actorId.Trim(),
            resourceKey.Trim(),
            actionType.Trim(),
            outcome.Trim(),
            reasonCode?.Trim() ?? string.Empty,
            reasonText?.Trim() ?? string.Empty);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private async Task WritePrivilegedAuditSafeAsync(
        string actorId,
        string actorRole,
        Guid? targetUserId,
        string actionType,
        string reasonCode,
        string reasonText,
        string outcome,
        DateTime occurredAtUtc,
        string correlationId,
        string traceId,
        string intentKey,
        bool required,
        CancellationToken cancellationToken)
    {
        PrivilegedAuditWriteAttemptedCounter.Add(1);

        var request = new PrivilegedAuditWriteRequest(
            actorId,
            actorRole,
            targetUserId,
            actionType,
            string.IsNullOrWhiteSpace(reasonCode) ? "unspecified" : reasonCode.Trim(),
            reasonText.Trim(),
            outcome,
            occurredAtUtc,
            ResolveCorrelationId(correlationId),
            traceId,
            intentKey);

        try
        {
            var result = await privilegedAuditWriter.AppendAsync(request, cancellationToken);
            if (result.AlreadyExists || string.Equals(result.Outcome, "alreadyApplied", StringComparison.OrdinalIgnoreCase))
            {
                PrivilegedAuditWriteRejectedCounter.Add(1);
            }
            else
            {
                PrivilegedAuditWriteSucceededCounter.Add(1);
            }
        }
        catch (Exception ex)
        {
            PrivilegedAuditWriteFailedCounter.Add(1);
            logger.LogError(
                ex,
                "Privileged audit write failed. ActorId: {ActorId}. ActionType: {ActionType}. Outcome: {Outcome}. TargetUserId: {TargetUserId}. TraceId: {TraceId}",
                actorId,
                actionType,
                outcome,
                targetUserId,
                traceId);

            if (required)
            {
                throw;
            }
        }
    }

    [HttpGet("admin/account-notifications/failures")]
    [Authorize(Policy = AppPolicies.AdminOnly)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAccountNotificationFailures(CancellationToken cancellationToken)
    {
        var failedStatuses = new[]
        {
            AccountNotificationDispatchStatus.FailedTransient,
            AccountNotificationDispatchStatus.FailedPermanent
        };

        var recent = await dbContext.AccountNotificationDispatches
            .AsNoTracking()
            .Where(dispatch => failedStatuses.Contains(dispatch.Status))
            .OrderByDescending(dispatch => dispatch.CreatedAtUtc)
            .Take(25)
            .Select(dispatch => new
            {
                dispatch.Id,
                dispatch.UserId,
                dispatch.EventType,
                dispatch.Status,
                dispatch.AttemptCount,
                dispatch.CreatedAtUtc,
                dispatch.LastUpdatedAtUtc,
                dispatch.TraceId,
                dispatch.CorrelationId,
                dispatch.LastFailureCategory
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            traceId = HttpContext.TraceIdentifier,
            failureCount = recent.Count,
            items = recent
        });
    }

    [HttpGet("admin/suspicious-cases")]
    [Authorize(Policy = AppPolicies.AuthenticatedUser)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSuspiciousCases([FromQuery] SuspiciousCaseQuery query, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role) ?? "unknown";

        SuspiciousCaseQueryCounter.Add(1);

        var authorization = await authorizationService.AuthorizeAsync(User, resource: null, policyName: AppPolicies.AdminOnly);
        if (!authorization.Succeeded)
        {
            SuspiciousCaseForbiddenCounter.Add(1);

            logger.LogWarning(
                "Suspicious-case review denied. ActorId: {ActorId}. ActorRole: {ActorRole}. TraceId: {TraceId}",
                actorId,
                actorRole,
                HttpContext.TraceIdentifier);

            return ForbiddenProblem();
        }

        if (!TryParseSuspiciousCaseQuery(query, out var anomalyType, out var page, out var pageSize, out var errors))
        {
            return ValidationProblem("validation.request.invalid", errors);
        }

        var result = await leaderboardRepository.GetSuspiciousActivityCasesAsync(
            anomalyType,
            page,
            pageSize,
            cancellationToken);

        if (result.TotalCount == 0)
        {
            SuspiciousCaseEmptyCounter.Add(1);
        }

        logger.LogInformation(
            "Suspicious-case review served. ActorId: {ActorId}. ActorRole: {ActorRole}. AnomalyType: {AnomalyType}. Page: {Page}. PageSize: {PageSize}. Total: {Total}. TraceId: {TraceId}",
            actorId,
            actorRole,
            anomalyType ?? "all",
            result.Page,
            result.PageSize,
            result.TotalCount,
            HttpContext.TraceIdentifier);

        return Ok(new SuspiciousCaseResponse(
            result.Page,
            result.PageSize,
            result.TotalCount,
            (result.Page * result.PageSize) < result.TotalCount,
            result.Items
                .Select(item =>
                {
                    var token = item.AnomalyType == "rankingMismatch"
                        ? BuildDestructiveConfirmationToken(actorId, item.CaseId)
                        : null;

                    return new SuspiciousCaseItemResponse(
                        item.CaseId,
                        item.PublicIdentity,
                        item.IdentityMode == LeaderboardIdentityMode.Public ? "public" : "anonymous",
                        item.AnomalyType,
                        item.SignalSummary,
                        item.Severity,
                        item.DetectedAtUtc,
                        item.LastActivityAtUtc,
                        item.CorrelationRef,
                        token);
                })
                .ToArray()));
    }

    [HttpPost("admin/suspicious-cases/{caseId}/moderation-actions")]
    [Authorize(Policy = AppPolicies.AuthenticatedUser)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApplyModerationAction(
        [FromRoute] string caseId,
        [FromBody] ModerationActionRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "unknown";
        var actorRole = User.FindFirstValue(ClaimTypes.Role) ?? "unknown";
        var correlationId = ResolveCorrelationId();
        ModerationAttemptedCounter.Add(1);

        var authorization = await authorizationService.AuthorizeAsync(User, resource: null, policyName: AppPolicies.AdminOnly);
        if (!authorization.Succeeded)
        {
            ModerationRejectedCounter.Add(1);
            TryExtractTargetUserId(caseId, out var deniedTargetUserId);
            await WritePrivilegedAuditSafeAsync(
                actorId,
                actorRole,
                deniedTargetUserId == Guid.Empty ? null : deniedTargetUserId,
                PrivilegedModerationActionType,
                string.IsNullOrWhiteSpace(request.ReasonCode) ? "missing-reason-code" : request.ReasonCode.Trim(),
                request.ReasonText?.Trim() ?? string.Empty,
                "forbidden",
                DateTime.UtcNow,
                correlationId,
                HttpContext.TraceIdentifier,
                ComputeAuditIntentKey(actorId, caseId, PrivilegedModerationActionType, "forbidden", request.ReasonCode, request.ReasonText),
                required: false,
                cancellationToken);

            logger.LogWarning(
                "Moderation action denied. ActorId: {ActorId}. ActorRole: {ActorRole}. CaseId: {CaseId}. TraceId: {TraceId}",
                actorId,
                actorRole,
                caseId,
                HttpContext.TraceIdentifier);

            return ForbiddenProblem();
        }

        if (!TryParseModerationRequest(request, out var normalizedActionType, out var reasonCode, out var reasonText, out var validationErrors))
        {
            ModerationRejectedCounter.Add(1);
            await WritePrivilegedAuditSafeAsync(
                actorId,
                actorRole,
                null,
                PrivilegedModerationActionType,
                string.IsNullOrWhiteSpace(request.ReasonCode) ? "validation-rejected" : request.ReasonCode.Trim(),
                request.ReasonText?.Trim() ?? string.Empty,
                "validationRejected",
                DateTime.UtcNow,
                correlationId,
                HttpContext.TraceIdentifier,
                ComputeAuditIntentKey(actorId, caseId, PrivilegedModerationActionType, "validationRejected", request.ReasonCode, request.ReasonText),
                required: false,
                cancellationToken);

            return ValidationProblem("validation.request.invalid", validationErrors);
        }

        var suspiciousCase = await leaderboardRepository.GetSuspiciousActivityCaseByIdAsync(caseId, cancellationToken);
        if (suspiciousCase is null)
        {
            ModerationRejectedCounter.Add(1);
            await WritePrivilegedAuditSafeAsync(
                actorId,
                actorRole,
                null,
                PrivilegedModerationActionType,
                reasonCode,
                reasonText,
                "targetNotFound",
                DateTime.UtcNow,
                correlationId,
                HttpContext.TraceIdentifier,
                ComputeAuditIntentKey(actorId, caseId, PrivilegedModerationActionType, "targetNotFound", reasonCode, reasonText),
                required: false,
                cancellationToken);

            return NotFoundProblem("ops.suspicious_case.not_found", "Suspicious case was not found or is no longer actionable.");
        }

        if (!TryExtractTargetUserId(caseId, out var targetUserId))
        {
            ModerationRejectedCounter.Add(1);
            await WritePrivilegedAuditSafeAsync(
                actorId,
                actorRole,
                null,
                PrivilegedModerationActionType,
                reasonCode,
                reasonText,
                "validationRejected",
                DateTime.UtcNow,
                correlationId,
                HttpContext.TraceIdentifier,
                ComputeAuditIntentKey(actorId, caseId, PrivilegedModerationActionType, "validationRejected", reasonCode, reasonText),
                required: false,
                cancellationToken);

            return ValidationProblem(
                "validation.request.invalid",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["caseId"] = ["The caseId route value is invalid."]
                });
        }

        if (normalizedActionType == RankingCorrectionActionType && !string.Equals(suspiciousCase.AnomalyType, "rankingMismatch", StringComparison.Ordinal))
        {
            ModerationRejectedCounter.Add(1);
            await WritePrivilegedAuditSafeAsync(
                actorId,
                actorRole,
                targetUserId,
                PrivilegedModerationActionType,
                reasonCode,
                reasonText,
                "validationRejected",
                DateTime.UtcNow,
                correlationId,
                HttpContext.TraceIdentifier,
                ComputeAuditIntentKey(actorId, caseId, PrivilegedModerationActionType, "validationRejected", reasonCode, reasonText),
                required: false,
                cancellationToken);

            return ValidationProblem(
                "validation.request.invalid",
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
                {
                    ["actionType"] = ["The rankingCorrection action is only valid for rankingMismatch cases."]
                });
        }

        if (normalizedActionType == RankingCorrectionActionType)
        {
            if (request.ConfirmDestructive is not true)
            {
                ModerationRejectedCounter.Add(1);
                await WritePrivilegedAuditSafeAsync(
                    actorId,
                    actorRole,
                    targetUserId,
                    PrivilegedModerationActionType,
                    reasonCode,
                    reasonText,
                    "confirmationRequired",
                    DateTime.UtcNow,
                    correlationId,
                    HttpContext.TraceIdentifier,
                    ComputeAuditIntentKey(actorId, caseId, PrivilegedModerationActionType, "confirmationRequired", reasonCode, reasonText),
                    required: false,
                    cancellationToken);

                return ConfirmationProblem(caseId, normalizedActionType, "Explicit confirmation is required before applying this ranking correction.");
            }

            if (!ValidateDestructiveConfirmationToken(actorId, caseId, request.ConfirmationToken))
            {
                ModerationRejectedCounter.Add(1);
                await WritePrivilegedAuditSafeAsync(
                    actorId,
                    actorRole,
                    targetUserId,
                    PrivilegedModerationActionType,
                    reasonCode,
                    reasonText,
                    "confirmationRequired",
                    DateTime.UtcNow,
                    correlationId,
                    HttpContext.TraceIdentifier,
                    ComputeAuditIntentKey(actorId, caseId, PrivilegedModerationActionType, "confirmationRequired", reasonCode, reasonText),
                    required: false,
                    cancellationToken);

                return ConfirmationProblem(caseId, normalizedActionType, "Confirmation token is missing, stale, or invalid.");
            }
        }

        var intentKey = ComputeIntentKey(actorId, targetUserId, caseId, normalizedActionType, reasonCode, reasonText);
        var now = DateTime.UtcNow;

        var existingIntent = await dbContext.ModerationActionAudits
            .AsNoTracking()
            .FirstOrDefaultAsync(audit => audit.IntentKey == intentKey, cancellationToken);

        if (existingIntent is not null)
        {
            ModerationRejectedCounter.Add(1);
            await WritePrivilegedAuditSafeAsync(
                actorId,
                actorRole,
                targetUserId,
                PrivilegedModerationActionType,
                reasonCode,
                reasonText,
                "alreadyApplied",
                existingIntent.CreatedAtUtc,
                correlationId,
                HttpContext.TraceIdentifier,
                intentKey,
                required: false,
                cancellationToken);

            logger.LogInformation(
                "Moderation action deduplicated. ActorId: {ActorId}. TargetUserId: {TargetUserId}. CaseId: {CaseId}. ActionType: {ActionType}. IntentKey: {IntentKey}. TraceId: {TraceId}",
                actorId,
                targetUserId,
                caseId,
                normalizedActionType,
                intentKey,
                HttpContext.TraceIdentifier);

            return Ok(new ModerationActionResponse(
                existingIntent.Id,
                caseId,
                normalizedActionType,
                "alreadyApplied",
                suspiciousCase.CorrelationRef,
                existingIntent.CreatedAtUtc,
                HttpContext.TraceIdentifier));
        }

        try
        {
            var targetUser = await dbContext.Users.FirstOrDefaultAsync(user => user.Id == targetUserId, cancellationToken);
            if (targetUser is null)
            {
                ModerationRejectedCounter.Add(1);
                return NotFoundProblem("ops.target.user.not_found", "The suspicious case target user no longer exists.");
            }

            var outcome = ApplyModerationMutation(targetUser, normalizedActionType, now)
                ? "succeeded"
                : "alreadyApplied";

            var audit = new ModerationActionAudit
            {
                Id = Guid.NewGuid(),
                CaseId = caseId,
                CorrelationRef = suspiciousCase.CorrelationRef,
                TargetUserId = targetUserId,
                ActorUserId = actorId,
                ActorRole = actorRole,
                ActionType = normalizedActionType,
                ReasonCode = reasonCode,
                ReasonText = reasonText,
                ConfirmDestructive = request.ConfirmDestructive is true,
                ConfirmationToken = request.ConfirmationToken,
                Outcome = outcome,
                IntentKey = intentKey,
                CreatedAtUtc = now,
                TraceId = HttpContext.TraceIdentifier
            };

            dbContext.ModerationActionAudits.Add(audit);
            await dbContext.SaveChangesAsync(cancellationToken);

            await WritePrivilegedAuditSafeAsync(
                actorId,
                actorRole,
                targetUserId,
                PrivilegedModerationActionType,
                reasonCode,
                reasonText,
                outcome,
                now,
                string.IsNullOrWhiteSpace(suspiciousCase.CorrelationRef) ? correlationId : suspiciousCase.CorrelationRef,
                HttpContext.TraceIdentifier,
                intentKey,
                required: true,
                cancellationToken);

            if (string.Equals(outcome, "succeeded", StringComparison.Ordinal))
            {
                ModerationSucceededCounter.Add(1);
            }
            else
            {
                ModerationRejectedCounter.Add(1);
            }

            logger.LogInformation(
                "Moderation action processed. ActorId: {ActorId}. TargetUserId: {TargetUserId}. ActionType: {ActionType}. Outcome: {Outcome}. CaseId: {CaseId}. CorrelationRef: {CorrelationRef}. TraceId: {TraceId}",
                actorId,
                targetUserId,
                normalizedActionType,
                outcome,
                caseId,
                suspiciousCase.CorrelationRef,
                HttpContext.TraceIdentifier);

            return Ok(new ModerationActionResponse(
                audit.Id,
                caseId,
                normalizedActionType,
                outcome,
                suspiciousCase.CorrelationRef,
                now,
                HttpContext.TraceIdentifier));
        }
        catch (DbUpdateException)
        {
            var conflictIntent = await dbContext.ModerationActionAudits
                .AsNoTracking()
                .FirstOrDefaultAsync(audit => audit.IntentKey == intentKey, cancellationToken);

            if (conflictIntent is not null)
            {
                ModerationRejectedCounter.Add(1);

                await WritePrivilegedAuditSafeAsync(
                    actorId,
                    actorRole,
                    targetUserId,
                    PrivilegedModerationActionType,
                    reasonCode,
                    reasonText,
                    "alreadyApplied",
                    conflictIntent.CreatedAtUtc,
                    correlationId,
                    HttpContext.TraceIdentifier,
                    intentKey,
                    required: false,
                    cancellationToken);

                return Ok(new ModerationActionResponse(
                    conflictIntent.Id,
                    caseId,
                    normalizedActionType,
                    "alreadyApplied",
                    suspiciousCase.CorrelationRef,
                    conflictIntent.CreatedAtUtc,
                    HttpContext.TraceIdentifier));
            }

            ModerationFailedCounter.Add(1);
            throw;
        }
        catch (Exception ex)
        {
            ModerationFailedCounter.Add(1);
            await WritePrivilegedAuditSafeAsync(
                actorId,
                actorRole,
                targetUserId,
                PrivilegedModerationActionType,
                reasonCode,
                reasonText,
                "failed",
                DateTime.UtcNow,
                correlationId,
                HttpContext.TraceIdentifier,
                ComputeAuditIntentKey(actorId, caseId, PrivilegedModerationActionType, "failed", reasonCode, reasonText),
                required: false,
                cancellationToken);

            logger.LogError(
                ex,
                "Moderation action failed. ActorId: {ActorId}. CaseId: {CaseId}. ActionType: {ActionType}. TraceId: {TraceId}",
                actorId,
                caseId,
                normalizedActionType,
                HttpContext.TraceIdentifier);
            throw;
        }
    }

    private static bool TryParseSuspiciousCaseQuery(
        SuspiciousCaseQuery query,
        out string? anomalyType,
        out int page,
        out int pageSize,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        anomalyType = null;
        if (!string.IsNullOrWhiteSpace(query.AnomalyType))
        {
            var normalized = query.AnomalyType.Trim();
            if (string.Equals(normalized, "activitySpike", StringComparison.OrdinalIgnoreCase))
            {
                anomalyType = "activitySpike";
            }
            else if (string.Equals(normalized, "rankingMismatch", StringComparison.OrdinalIgnoreCase))
            {
                anomalyType = "rankingMismatch";
            }
            else
            {
                errors["anomalyType"] = ["The anomalyType field must be one of: activitySpike, rankingMismatch."];
            }
        }

        page = query.Page ?? DefaultPage;
        if (page < MinPage)
        {
            errors["page"] = [$"The page field must be greater than or equal to {MinPage}."];
        }

        pageSize = query.PageSize ?? DefaultPageSize;
        if (pageSize < MinPageSize || pageSize > MaxPageSize)
        {
            errors["pageSize"] = [$"The pageSize field must be between {MinPageSize} and {MaxPageSize}."];
        }

        return errors.Count == 0;
    }

    private static bool TryParseModerationRequest(
        ModerationActionRequest request,
        out string actionType,
        out string reasonCode,
        out string reasonText,
        out Dictionary<string, string[]> errors)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        actionType = string.Empty;
        reasonCode = string.Empty;
        reasonText = string.Empty;

        if (string.IsNullOrWhiteSpace(request.ActionType))
        {
            errors["actionType"] = ["The actionType field is required."];
        }
        else
        {
            var normalizedAction = request.ActionType.Trim();
            if (string.Equals(normalizedAction, RankingCorrectionActionType, StringComparison.OrdinalIgnoreCase))
            {
                actionType = RankingCorrectionActionType;
            }
            else if (string.Equals(normalizedAction, FlagEntityActionType, StringComparison.OrdinalIgnoreCase))
            {
                actionType = FlagEntityActionType;
            }
            else
            {
                errors["actionType"] = ["The actionType field must be one of: rankingCorrection, flagEntity."];
            }
        }

        if (string.IsNullOrWhiteSpace(request.ReasonCode))
        {
            errors["reasonCode"] = ["The reasonCode field is required."];
        }
        else
        {
            reasonCode = request.ReasonCode.Trim();
            if (reasonCode.Length > ReasonCodeMaxLength)
            {
                errors["reasonCode"] = [$"The reasonCode field must be {ReasonCodeMaxLength} characters or fewer."];
            }
        }

        reasonText = request.ReasonText?.Trim() ?? string.Empty;
        if (reasonText.Length > ReasonTextMaxLength)
        {
            errors["reasonText"] = [$"The reasonText field must be {ReasonTextMaxLength} characters or fewer."];
        }

        if (request.ConfirmationToken is not null && request.ConfirmationToken.Length > 256)
        {
            errors["confirmationToken"] = ["The confirmationToken field must be 256 characters or fewer."];
        }

        return errors.Count == 0;
    }

    private static bool ApplyModerationMutation(User targetUser, string actionType, DateTime now)
    {
        switch (actionType)
        {
            case RankingCorrectionActionType:
                if (targetUser.LeaderboardParticipationMode == LeaderboardParticipationMode.Hidden)
                {
                    return false;
                }

                targetUser.LeaderboardParticipationMode = LeaderboardParticipationMode.Hidden;
                targetUser.ModifiedAtUtc = now;
                return true;
            case FlagEntityActionType:
                if (targetUser.IsSuspiciousFlagged)
                {
                    return false;
                }

                targetUser.IsSuspiciousFlagged = true;
                targetUser.ModifiedAtUtc = now;
                return true;
            default:
                return false;
        }
    }

    private static bool TryExtractTargetUserId(string caseId, out Guid userId)
    {
        userId = Guid.Empty;

        if (string.IsNullOrWhiteSpace(caseId))
        {
            return false;
        }

        const string rankingPrefix = "ranking-mismatch-";
        const string spikePrefix = "activity-spike-";

        if (caseId.StartsWith(rankingPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Guid.TryParseExact(caseId[rankingPrefix.Length..], "N", out userId);
        }

        if (caseId.StartsWith(spikePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Guid.TryParseExact(caseId[spikePrefix.Length..], "N", out userId);
        }

        return false;
    }

    private static string ComputeIntentKey(
        string actorId,
        Guid targetUserId,
        string caseId,
        string actionType,
        string reasonCode,
        string reasonText)
    {
        var normalized = string.Join('|',
            actorId.Trim(),
            targetUserId.ToString("N"),
            caseId.Trim(),
            actionType.Trim(),
            reasonCode.Trim(),
            reasonText.Trim());

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static string BuildDestructiveConfirmationToken(string actorId, string caseId)
    {
        var bucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (long)ConfirmationWindow.TotalSeconds;
        return BuildDestructiveConfirmationToken(actorId, caseId, bucket);
    }

    private static string BuildDestructiveConfirmationToken(string actorId, string caseId, long bucket)
    {
        var payload = $"{actorId.Trim()}|{caseId.Trim()}|{RankingCorrectionActionType}|{bucket}";
        var digest = HMACSHA256.HashData(ConfirmationSecret, Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static bool ValidateDestructiveConfirmationToken(string actorId, string caseId, string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var provided = token.Trim();
        var currentBucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (long)ConfirmationWindow.TotalSeconds;

        var currentToken = BuildDestructiveConfirmationToken(actorId, caseId, currentBucket);
        if (string.Equals(currentToken, provided, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var previousToken = BuildDestructiveConfirmationToken(actorId, caseId, currentBucket - 1);
        return string.Equals(previousToken, provided, StringComparison.OrdinalIgnoreCase);
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

    private ObjectResult ForbiddenProblem()
    {
        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/forbidden",
            Title = "Forbidden",
            Status = StatusCodes.Status403Forbidden
        };

        details.Extensions["code"] = "authz.access.denied";
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

    private ObjectResult ConfirmationProblem(string caseId, string actionType, string detail)
    {
        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/confirmation-required",
            Title = "Confirmation Required",
            Status = StatusCodes.Status409Conflict,
            Detail = detail
        };

        details.Extensions["code"] = "ops.moderation.confirmation_required";
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;
        details.Extensions["recovery"] = "Reload suspicious cases to fetch a fresh confirmation token and resubmit with confirmDestructive=true.";
        details.Extensions["caseId"] = caseId;
        details.Extensions["actionType"] = actionType;

        return StatusCode(StatusCodes.Status409Conflict, details);
    }

    public sealed record SuspiciousCaseQuery(string? AnomalyType, int? Page, int? PageSize);

    public sealed record SupportDiagnosticQuery(int? WindowDays, int? MarkerLimit);

    public sealed record SupportTimelineQuery(string? EventType, DateTime? StartUtc, DateTime? EndUtc, int? Page, int? MaxItems);

    public sealed record PrivilegedAuditQuery(
        string? ActorUserId,
        string? TargetUserId,
        string? ActionType,
        DateTime? StartUtc,
        DateTime? EndUtc,
        int? Page,
        int? PageSize);

    public sealed record IntegrationFailureQuery(
        string? IntegrationId,
        string? OwnerUserId,
        string? ErrorClass,
        string? CorrelationId,
        string? TraceId,
        DateTime? StartUtc,
        DateTime? EndUtc,
        int? Page,
        int? PageSize);

    public sealed record SupportUserDiagnosticResponse(
        SupportAccountSnapshot Account,
        SupportTaskStateSnapshot TaskState,
        SupportXpStateSnapshot XpState,
        SupportStreakStateSnapshot StreakState,
        SupportDiagnosticWindow Window,
        IReadOnlyCollection<SupportProgressMarker> RecentMarkers,
        string CorrelationId,
        string TraceId);

    public sealed record SupportAccountSnapshot(
        Guid UserId,
        string Email,
        string DisplayName,
        string Role,
        string TimeZoneId,
        string Locale,
        string LeaderboardParticipationMode,
        bool IsSuspiciousFlagged,
        DateTime CreatedAtUtc,
        DateTime ModifiedAtUtc);

    public sealed record SupportTaskStateSnapshot(
        int TotalCount,
        int CompletedCount,
        int ActiveCount,
        DateTime? LastCompletedAtUtc,
        IReadOnlyCollection<SupportRecentCompletion> RecentCompletions);

    public sealed record SupportRecentCompletion(
        Guid TaskId,
        string Title,
        DateTime CompletedAtUtc);

    public sealed record SupportXpStateSnapshot(
        int TotalXp,
        int LedgerEntryCount,
        DateTime? LastGrantedAtUtc,
        string OutcomeReasonCode,
        string OutcomeExplanation);

    public sealed record SupportStreakStateSnapshot(
        string Outcome,
        int CurrentStreakDays,
        int LongestStreakDays,
        string TimeZoneId,
        DateTime EvaluationWindowStartUtc,
        DateTime EvaluationWindowEndUtc,
        DateTime LastEvaluatedAtUtc,
        string OutcomeReasonCode,
        string OutcomeExplanation,
        bool IsRecoveryPromptVisible,
        string? RecoveryReason,
        string? RecommendedAction,
        string? RecoveryExplanation);

    public sealed record SupportDiagnosticWindow(
        int WindowDays,
        DateTime WindowStartUtc,
        int MarkerLimit);

    private sealed record ParsedSupportTimelineQuery(
        string? EventType,
        DateTime StartUtc,
        DateTime EndUtc,
        int Page,
        int MaxItems);

    private sealed record ParsedPrivilegedAuditQuery(
        string? ActorUserId,
        Guid? TargetUserId,
        string? ActionType,
        DateTime StartUtc,
        DateTime EndUtc,
        int Page,
        int PageSize);

    private sealed record ParsedIntegrationFailureQuery(
        string? IntegrationId,
        Guid? OwnerUserId,
        string? ErrorClass,
        string? CorrelationId,
        string? TraceId,
        DateTime StartUtc,
        DateTime EndUtc,
        int Page,
        int PageSize);

    public sealed record PrivilegedAuditFilterEnvelope(
        string? ActorUserId,
        Guid? TargetUserId,
        string? ActionType,
        DateTime StartUtc,
        DateTime EndUtc);

    public sealed record PrivilegedAuditItem(
        Guid AuditId,
        string ActorUserId,
        string ActorRole,
        Guid? TargetUserId,
        string ActionType,
        string ReasonCode,
        string ReasonText,
        string Outcome,
        DateTime OccurredAtUtc,
        string CorrelationId,
        string TraceId);

    public sealed record PrivilegedAuditQueryResponse(
        int Page,
        int PageSize,
        int TotalCount,
        bool HasNextPage,
        PrivilegedAuditFilterEnvelope Filters,
        IReadOnlyCollection<PrivilegedAuditItem> Items,
        string CorrelationId,
        string TraceId);

    public sealed record IntegrationFailureFilterEnvelope(
        string? IntegrationId,
        Guid? OwnerUserId,
        string? ErrorClass,
        string? CorrelationId,
        string? TraceId,
        DateTime StartUtc,
        DateTime EndUtc);

    public sealed record IntegrationFailureItem(
        Guid FailureEventId,
        DateTime OccurredAtUtc,
        string IntegrationId,
        Guid OwnerUserId,
        string? ExternalTaskId,
        string? IdempotencyKey,
        string ErrorClass,
        string ErrorCode,
        int HttpStatus,
        string CorrelationId,
        string TraceId);

    public sealed record IntegrationFailureQueryResponse(
        int Page,
        int PageSize,
        int TotalCount,
        bool HasNextPage,
        IntegrationFailureFilterEnvelope Filters,
        IReadOnlyCollection<IntegrationFailureItem> Items,
        string CorrelationId,
        string TraceId);

    public sealed record SupportTimelineFilterEnvelope(
        string? EventType,
        DateTime StartUtc,
        DateTime EndUtc);

    public sealed record SupportTimelineEvent(
        string EventId,
        string EventType,
        DateTime OccurredAtUtc,
        string SourceSubsystem,
        string MessageCode,
        string Message,
        string RuleOutcome,
        string? TraceId,
        string? CorrelationId,
        string ActorContext,
        string TargetContext,
        string? RelatedEntityId);

    public sealed record SupportTimelineResponse(
        int Page,
        int PageSize,
        int TotalCount,
        bool HasNextPage,
        SupportTimelineFilterEnvelope Filters,
        IReadOnlyCollection<SupportTimelineEvent> Items,
        string CorrelationId,
        string TraceId);

    public sealed record SupportProgressMarker(
        string MarkerType,
        string MarkerId,
        DateTime OccurredAtUtc,
        string Summary,
        string? TraceId,
        string? CorrelationRef);

    public sealed record SuspiciousCaseResponse(
        int Page,
        int PageSize,
        int TotalCount,
        bool HasNextPage,
        IReadOnlyCollection<SuspiciousCaseItemResponse> Items);

    public sealed record SuspiciousCaseItemResponse(
        string CaseId,
        string PublicIdentity,
        string IdentityMode,
        string AnomalyType,
        string SignalSummary,
        int Severity,
        DateTime DetectedAtUtc,
        DateTime? LastActivityAtUtc,
        string CorrelationRef,
        string? DestructiveConfirmationToken);

    public sealed record ModerationActionRequest(
        string ActionType,
        string ReasonCode,
        string? ReasonText,
        bool? ConfirmDestructive,
        string? ConfirmationToken);

    public sealed record ModerationActionResponse(
        Guid AuditId,
        string CaseId,
        string ActionType,
        string Outcome,
        string CorrelationRef,
        DateTime ProcessedAtUtc,
        string TraceId);
}
