using System.Security.Claims;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Integrations.Authentication;
using TaskTracker.Api.Features.Integrations.Contracts;
using TaskTracker.Api.Features.Integrations.Services;
using TaskTracker.Api.Features.Tasks.Repositories;
using TaskTracker.Api.Features.Tasks.Validation;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Controllers;

[ApiController]
[Route("api/v1/integrations")]
public class IntegrationsController(
    IIntegrationCredentialService integrationCredentialService,
    ITaskRepository taskRepository,
    TaskTrackerDbContext dbContext,
    ILogger<IntegrationsController> logger) : ControllerBase
{
    private const int IntegrationIdMaxLength = 64;
    private const int IntegrationNameMaxLength = 128;
    private const int ExternalTaskIdMaxLength = 160;
    private const int CorrelationIdMaxLength = 128;
    private const int TraceIdMaxLength = 128;
    private const int IdempotencyKeyMaxLength = 64;
    private const string IntegrationTaskCreateSyncIdempotencyHeader = "Idempotency-Key";

    private static readonly Meter Meter = new("TaskTracker.Api.Integrations", "1.0.0");
    private static readonly Counter<long> TaskCreateSyncRequestCounter =
        Meter.CreateCounter<long>("integrations.tasks.create_sync.request.total");
    private static readonly Counter<long> TaskCreateSyncOutcomeCounter =
        Meter.CreateCounter<long>("integrations.tasks.create_sync.outcome.total");
    private static readonly Counter<long> TaskCreateSyncRetryShapeCounter =
        Meter.CreateCounter<long>("integrations.tasks.create_sync.retry_shape.total");
    private static readonly Histogram<double> TaskCreateSyncLatencyMs =
        Meter.CreateHistogram<double>("integrations.tasks.create_sync.latency_ms");

    [HttpPost("credentials")]
    [Authorize(Policy = AppPolicies.AuthenticatedUser)]
    [ProducesResponseType<IntegrationCredentialCreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateCredential(
        [FromBody] CreateIntegrationCredentialRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("integrations.identity.invalid");
        }

        if (!TryValidateCreateRequest(request, out var errors, out var integrationId, out var integrationName, out var scopes))
        {
            return ValidationProblem("validation.request.invalid", errors);
        }

        var issueResult = await integrationCredentialService.IssueAsync(
            userId,
            integrationId!,
            integrationName!,
            scopes!,
            request.ExpiresAtUtc,
            cancellationToken);

        logger.LogInformation(
            "Integration credential issued. OwnerUserId: {OwnerUserId}. CredentialId: {CredentialId}. IntegrationId: {IntegrationId}. ScopeCount: {ScopeCount}. TraceId: {TraceId}",
            userId,
            issueResult.Credential.Id,
            issueResult.Credential.IntegrationId,
            issueResult.Scopes.Count,
            HttpContext.TraceIdentifier);

        return Created(
            $"/api/v1/integrations/credentials/{issueResult.Credential.Id}",
            new IntegrationCredentialCreatedResponse(
                issueResult.Credential.Id,
                issueResult.Credential.KeyId,
                issueResult.Credential.IntegrationId,
                issueResult.Credential.IntegrationName,
                issueResult.Credential.OwnerUserId,
                issueResult.Scopes.OrderBy(scope => scope, StringComparer.Ordinal).ToArray(),
                issueResult.Credential.CreatedAtUtc,
                issueResult.Credential.ExpiresAtUtc,
                issueResult.PlainTextSecret,
                HttpContext.TraceIdentifier));
    }

    [HttpGet("credentials")]
    [Authorize(Policy = AppPolicies.AuthenticatedUser)]
    [ProducesResponseType<IntegrationCredentialListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListCredentials(CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("integrations.identity.invalid");
        }

        var credentials = await integrationCredentialService.ListOwnedAsync(userId, cancellationToken);

        var response = new IntegrationCredentialListResponse(
            credentials
                .Select(item => new IntegrationCredentialListItemResponse(
                    item.Credential.Id,
                    item.Credential.KeyId,
                    item.Credential.IntegrationId,
                    item.Credential.IntegrationName,
                    item.Credential.OwnerUserId,
                    item.Credential.Status == IntegrationCredentialStatus.Active ? "active" : "revoked",
                    item.Scopes.OrderBy(scope => scope, StringComparer.Ordinal).ToArray(),
                    item.Credential.CreatedAtUtc,
                    item.Credential.ExpiresAtUtc,
                    item.Credential.RevokedAtUtc,
                    item.Credential.RotatedAtUtc,
                    item.Credential.LastUsedAtUtc))
                .ToArray());

        return Ok(response);
    }

    [HttpDelete("credentials/{credentialId:guid}")]
    [Authorize(Policy = AppPolicies.AuthenticatedUser)]
    [ProducesResponseType<IntegrationCredentialRevokedResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeCredential(
        [FromRoute] Guid credentialId,
        CancellationToken cancellationToken)
    {
        if (!TryResolveCurrentUserId(out var userId))
        {
            return UnauthorizedProblem("integrations.identity.invalid");
        }

        var result = await integrationCredentialService.RevokeOwnedAsync(userId, credentialId, cancellationToken);

        if (result.Status == IntegrationCredentialRevocationStatus.NotFound || result.Credential is null)
        {
            return NotFoundProblem("integrations.credential.not_found", "Integration credential could not be found.");
        }

        logger.LogInformation(
            "Integration credential revoked. OwnerUserId: {OwnerUserId}. CredentialId: {CredentialId}. TraceId: {TraceId}",
            userId,
            credentialId,
            HttpContext.TraceIdentifier);

        return Ok(new IntegrationCredentialRevokedResponse(
            result.Credential.Id,
            "revoked",
            result.Credential.RevokedAtUtc ?? DateTime.UtcNow,
            HttpContext.TraceIdentifier));
    }

    [HttpPost("tasks/create-sync")]
    [Authorize(Policy = AppPolicies.IntegrationTaskCreateSync)]
    [ProducesResponseType<IntegrationTaskCreateSyncResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateTaskSync(
        [FromBody] IntegrationTaskCreateSyncRequest request,
        CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        var integrationId = User.FindFirstValue("integration_id") ?? "unknown";
        var ownerUserIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(ownerUserIdRaw, out var ownerUserId))
        {
            return UnauthorizedProblem("integrations.identity.invalid");
        }

        var correlationId = ResolveCorrelationId();
        var externalTaskId = request.ExternalTaskId?.Trim();
        var idempotencyKey = NormalizeRequestIdempotencyKey(Request.Headers[IntegrationTaskCreateSyncIdempotencyHeader].FirstOrDefault());

        TaskCreateSyncRequestCounter.Add(
            1,
            KeyValuePair.Create<string, object?>("integration_id", integrationId));

        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!TryResolveIdempotencyKey(out _, out var idempotencyError))
        {
            errors["idempotencyKey"] = [idempotencyError!];
        }

        if (string.IsNullOrWhiteSpace(externalTaskId))
        {
            errors["externalTaskId"] = ["The externalTaskId field is required."];
        }
        else if (externalTaskId.Length > ExternalTaskIdMaxLength)
        {
            errors["externalTaskId"] = [$"The externalTaskId field must be {ExternalTaskIdMaxLength} characters or fewer."];
        }

        var payloadValidation = TaskPayloadValidator.Validate(
            request.Title,
            request.Description,
            request.DueAtUtc,
            request.Priority,
            request.Category,
            request.Difficulty,
            request.EnergyLevel,
            request.ContextTag,
            request.EffortPoints);

        foreach (var pair in payloadValidation.Errors)
        {
            errors[pair.Key] = pair.Value;
        }

        if (errors.Count > 0)
        {
            const string errorClass = "validation";
            TaskCreateSyncOutcomeCounter.Add(
                1,
                KeyValuePair.Create<string, object?>("outcome", "validation_failed"),
                KeyValuePair.Create<string, object?>("integration_id", integrationId),
                KeyValuePair.Create<string, object?>("error_class", errorClass));

            var retryShape = await ResolveRetryShapeAsync(
                ownerUserId,
                integrationId,
                externalTaskId,
                defaultShape: "first_attempt",
                cancellationToken);

            TaskCreateSyncRetryShapeCounter.Add(
                1,
                KeyValuePair.Create<string, object?>("integration_id", integrationId),
                KeyValuePair.Create<string, object?>("retry_shape", retryShape));

            TaskCreateSyncLatencyMs.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                KeyValuePair.Create<string, object?>("integration_id", integrationId),
                KeyValuePair.Create<string, object?>("outcome", "validation_failed"),
                KeyValuePair.Create<string, object?>("error_class", errorClass));

            await RecordFailureEventSafeAsync(
                ownerUserId,
                integrationId,
                externalTaskId,
                idempotencyKey,
                errorClass,
                "validation.request.invalid",
                StatusCodes.Status400BadRequest,
                correlationId,
                cancellationToken);

            return ValidationProblem(
                "validation.request.invalid",
                errors,
                errorClass,
                "Fix request validation errors and retry.");
        }

        try
        {
            var syncResult = await taskRepository.UpsertOwnedFromIntegrationAsync(
                ownerUserId,
                integrationId,
                idempotencyKey!,
                externalTaskId!,
                request.Title!.Trim(),
                request.Description?.Trim() ?? string.Empty,
                request.DueAtUtc,
                request.Priority!.Trim().ToLowerInvariant(),
                request.Category!.Trim().ToLowerInvariant(),
                NormalizeDifficulty(request.Difficulty),
                NormalizeEnergyLevel(request.EnergyLevel),
                NormalizeContextTag(request.ContextTag),
                request.EffortPoints,
                request.IsCompleted ?? false,
                correlationId,
                HttpContext.TraceIdentifier,
                DateTime.UtcNow,
                cancellationToken);

            if (syncResult.Status == IntegrationTaskSyncStatus.Forbidden || syncResult.Task is null)
            {
                const string errorClass = "authorization";

                TaskCreateSyncOutcomeCounter.Add(
                    1,
                    KeyValuePair.Create<string, object?>("outcome", "forbidden"),
                    KeyValuePair.Create<string, object?>("integration_id", integrationId),
                    KeyValuePair.Create<string, object?>("error_class", errorClass));

                var retryShape = await ResolveRetryShapeAsync(
                    ownerUserId,
                    integrationId,
                    externalTaskId,
                    defaultShape: "first_attempt",
                    cancellationToken);

                TaskCreateSyncRetryShapeCounter.Add(
                    1,
                    KeyValuePair.Create<string, object?>("integration_id", integrationId),
                    KeyValuePair.Create<string, object?>("retry_shape", retryShape));

                TaskCreateSyncLatencyMs.Record(
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                    KeyValuePair.Create<string, object?>("integration_id", integrationId),
                    KeyValuePair.Create<string, object?>("outcome", "forbidden"),
                    KeyValuePair.Create<string, object?>("error_class", errorClass));

                await RecordFailureEventSafeAsync(
                    ownerUserId,
                    integrationId,
                    externalTaskId,
                    idempotencyKey,
                    errorClass,
                    "auth.forbidden",
                    StatusCodes.Status403Forbidden,
                    correlationId,
                    cancellationToken);

                return ForbiddenProblem(
                    "auth.forbidden",
                    errorClass,
                    "Ensure the integration credential has required scope and owner access.");
            }

            var operation = syncResult.Status switch
            {
                IntegrationTaskSyncStatus.Created => "created",
                IntegrationTaskSyncStatus.Updated => "updated",
                IntegrationTaskSyncStatus.IdempotentReplay => "idempotent_replay",
                _ => "unknown"
            };

            TaskCreateSyncOutcomeCounter.Add(
                1,
                KeyValuePair.Create<string, object?>("outcome", operation),
                KeyValuePair.Create<string, object?>("integration_id", integrationId),
                KeyValuePair.Create<string, object?>("error_class", "none"));

            var retryShapeForSuccess = syncResult.Status == IntegrationTaskSyncStatus.IdempotentReplay
                ? "idempotent_replay"
                : await ResolveRetryShapeAsync(
                    ownerUserId,
                    integrationId,
                    externalTaskId,
                    defaultShape: "first_attempt",
                    cancellationToken);

            TaskCreateSyncRetryShapeCounter.Add(
                1,
                KeyValuePair.Create<string, object?>("integration_id", integrationId),
                KeyValuePair.Create<string, object?>("retry_shape", retryShapeForSuccess));

            TaskCreateSyncLatencyMs.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                KeyValuePair.Create<string, object?>("integration_id", integrationId),
                KeyValuePair.Create<string, object?>("outcome", operation),
                KeyValuePair.Create<string, object?>("error_class", "none"));

            logger.LogInformation(
                "Integration create-sync processed. Outcome: {Outcome}. Replay: {Replay}. RetryShape: {RetryShape}. IntegrationId: {IntegrationId}. OwnerUserId: {OwnerUserId}. ExternalTaskId: {ExternalTaskId}. IdempotencyKey: {IdempotencyKey}. TaskId: {TaskId}. CorrelationId: {CorrelationId}. TraceId: {TraceId}",
                operation,
                syncResult.Status == IntegrationTaskSyncStatus.IdempotentReplay,
                retryShapeForSuccess,
                integrationId,
                ownerUserId,
                syncResult.ExternalTaskId ?? externalTaskId,
                idempotencyKey,
                syncResult.Task.Id,
                correlationId,
                HttpContext.TraceIdentifier);

            return Ok(new IntegrationTaskCreateSyncResponse(
                operation,
                syncResult.Status == IntegrationTaskSyncStatus.IdempotentReplay,
                integrationId,
                ownerUserId,
                syncResult.Task.Id,
                syncResult.ExternalTaskId ?? externalTaskId!,
                null,
                null,
                correlationId,
                HttpContext.TraceIdentifier));
        }
        catch (Exception ex)
        {
            var classifiedFailure = ClassifyFailure(ex);
            var retryShape = await ResolveRetryShapeAsync(
                ownerUserId,
                integrationId,
                externalTaskId,
                defaultShape: "first_attempt",
                cancellationToken);

            TaskCreateSyncOutcomeCounter.Add(
                1,
                KeyValuePair.Create<string, object?>("outcome", "server_failure"),
                KeyValuePair.Create<string, object?>("integration_id", integrationId),
                KeyValuePair.Create<string, object?>("error_class", classifiedFailure.ErrorClass));

            TaskCreateSyncRetryShapeCounter.Add(
                1,
                KeyValuePair.Create<string, object?>("integration_id", integrationId),
                KeyValuePair.Create<string, object?>("retry_shape", retryShape));

            TaskCreateSyncLatencyMs.Record(
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                KeyValuePair.Create<string, object?>("integration_id", integrationId),
                KeyValuePair.Create<string, object?>("outcome", "server_failure"),
                KeyValuePair.Create<string, object?>("error_class", classifiedFailure.ErrorClass));

            await RecordFailureEventSafeAsync(
                ownerUserId,
                integrationId,
                externalTaskId,
                idempotencyKey,
                classifiedFailure.ErrorClass,
                classifiedFailure.ErrorCode,
                classifiedFailure.HttpStatus,
                correlationId,
                cancellationToken);

            logger.LogError(
                ex,
                "Integration create-sync failed. ErrorClass: {ErrorClass}. ErrorCode: {ErrorCode}. IntegrationId: {IntegrationId}. OwnerUserId: {OwnerUserId}. ExternalTaskId: {ExternalTaskId}. CorrelationId: {CorrelationId}. TraceId: {TraceId}",
                classifiedFailure.ErrorClass,
                classifiedFailure.ErrorCode,
                integrationId,
                ownerUserId,
                externalTaskId ?? "unknown",
                correlationId,
                HttpContext.TraceIdentifier);

            return FailureProblem(
                classifiedFailure.ErrorCode,
                classifiedFailure.ErrorClass,
                classifiedFailure.HttpStatus,
                classifiedFailure.RecoveryHint);
        }
    }

    private static string? NormalizeRequestIdempotencyKey(string? rawIdempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(rawIdempotencyKey))
        {
            return null;
        }

        var normalized = rawIdempotencyKey.Trim();
        if (Guid.TryParse(normalized, out var parsed))
        {
            return parsed.ToString("D");
        }

        return LimitLength(normalized, IdempotencyKeyMaxLength);
    }

    private static TaskDifficulty NormalizeDifficulty(string? difficulty)
    {
        var normalizedDifficulty = string.IsNullOrWhiteSpace(difficulty)
            ? "easy"
            : difficulty.Trim().ToLowerInvariant();

        return normalizedDifficulty switch
        {
            "hard" => TaskDifficulty.Hard,
            "medium" => TaskDifficulty.Medium,
            _ => TaskDifficulty.Easy
        };
    }

    private static TaskEnergyLevel NormalizeEnergyLevel(string? energyLevel)
    {
        var normalizedEnergyLevel = string.IsNullOrWhiteSpace(energyLevel)
            ? "medium"
            : energyLevel.Trim().ToLowerInvariant();

        return normalizedEnergyLevel switch
        {
            "low" => TaskEnergyLevel.Low,
            "high" => TaskEnergyLevel.High,
            _ => TaskEnergyLevel.Medium
        };
    }

    private static string? NormalizeContextTag(string? contextTag)
    {
        if (string.IsNullOrWhiteSpace(contextTag))
        {
            return null;
        }

        return contextTag.Trim().ToLowerInvariant();
    }

    private static bool IsTransientInfrastructure(Exception ex)
    {
        if (ex is TimeoutException)
        {
            return true;
        }

        if (ex is DbUpdateException dbUpdateException
            && dbUpdateException.InnerException is SqlException sqlException)
        {
            return sqlException.Number is -2 or 1205;
        }

        return false;
    }

    private static ClassifiedFailure ClassifyFailure(Exception ex)
    {
        if (ex is ArgumentException)
        {
            return new ClassifiedFailure(
                "validation",
                "validation.request.invalid",
                StatusCodes.Status400BadRequest,
                "Fix request validation errors and retry.");
        }

        if (ex is KeyNotFoundException)
        {
            return new ClassifiedFailure(
                "not_found",
                "integrations.tasks.create_sync.not_found",
                StatusCodes.Status404NotFound,
                "Verify identifiers and retry with existing resources.");
        }

        if (IsTransientInfrastructure(ex))
        {
            return new ClassifiedFailure(
                "transient_infrastructure",
                "integrations.tasks.create_sync.transient_failure",
                StatusCodes.Status503ServiceUnavailable,
                "Retry later; this failure is usually transient.");
        }

        if (ex is DbUpdateException)
        {
            return new ClassifiedFailure(
                "conflict",
                "integrations.tasks.create_sync.conflict",
                StatusCodes.Status409Conflict,
                "Retry with a fresh idempotency key after reconciling conflicting data.");
        }

        return new ClassifiedFailure(
            "unexpected",
            "integrations.tasks.create_sync.unexpected",
            StatusCodes.Status500InternalServerError,
            "Retry later; if this persists, contact support with traceId and correlationId.");
    }

    private async Task<string> ResolveRetryShapeAsync(
        Guid ownerUserId,
        string integrationId,
        string? externalTaskId,
        string defaultShape,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalTaskId))
        {
            return defaultShape;
        }

        var hadPreviousFailure = await dbContext.IntegrationProcessingFailureEvents
            .AsNoTracking()
            .AnyAsync(item =>
                item.OwnerUserId == ownerUserId
                && item.IntegrationId == integrationId
                && item.ExternalTaskId == externalTaskId,
                cancellationToken);

        return hadPreviousFailure ? "post_failure_retry" : defaultShape;
    }

    private async Task RecordFailureEventSafeAsync(
        Guid ownerUserId,
        string integrationId,
        string? externalTaskId,
        string? idempotencyKey,
        string errorClass,
        string errorCode,
        int httpStatus,
        string correlationId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Prevent unrelated tracked entities from affecting observability write.
            dbContext.ChangeTracker.Clear();

            dbContext.IntegrationProcessingFailureEvents.Add(new IntegrationProcessingFailureEvent
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                IntegrationId = LimitLength(integrationId, IntegrationIdMaxLength) ?? integrationId,
                OwnerUserId = ownerUserId,
                ExternalTaskId = LimitLength(externalTaskId, ExternalTaskIdMaxLength),
                IdempotencyKey = LimitLength(idempotencyKey, IdempotencyKeyMaxLength),
                ErrorClass = errorClass,
                ErrorCode = errorCode,
                HttpStatus = httpStatus,
                CorrelationId = LimitLength(correlationId, CorrelationIdMaxLength) ?? HttpContext.TraceIdentifier ?? "n/a",
                TraceId = LimitLength(HttpContext.TraceIdentifier, TraceIdMaxLength) ?? "n/a"
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Integration failure event persistence skipped. IntegrationId: {IntegrationId}. OwnerUserId: {OwnerUserId}. ErrorClass: {ErrorClass}. ErrorCode: {ErrorCode}. CorrelationId: {CorrelationId}. TraceId: {TraceId}",
                integrationId,
                ownerUserId,
                errorClass,
                errorCode,
                correlationId,
                HttpContext.TraceIdentifier);
        }
    }

    private static string? LimitLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }

    private bool TryResolveIdempotencyKey(out string? idempotencyKey, out string? error)
    {
        idempotencyKey = null;
        error = null;

        if (!Request.Headers.TryGetValue(IntegrationTaskCreateSyncIdempotencyHeader, out var values))
        {
            error = "Idempotency-Key header is required for integration create-sync.";
            return false;
        }

        var normalized = values.FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            error = "Idempotency-Key header is required for integration create-sync.";
            return false;
        }

        if (!Guid.TryParse(normalized, out var parsed))
        {
            error = "Idempotency-Key header must be a valid GUID.";
            return false;
        }

        idempotencyKey = parsed.ToString("D");
        return true;
    }

    private bool TryResolveCurrentUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(userIdClaim, out userId);
    }

    private bool TryValidateCreateRequest(
        CreateIntegrationCredentialRequest request,
        out Dictionary<string, string[]> errors,
        out string? integrationId,
        out string? integrationName,
        out string[]? scopes)
    {
        errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        integrationId = request.IntegrationId?.Trim().ToLowerInvariant();
        integrationName = request.IntegrationName?.Trim();

        if (string.IsNullOrWhiteSpace(integrationId))
        {
            errors["integrationId"] = ["The integrationId field is required."];
        }
        else if (integrationId.Length > IntegrationIdMaxLength)
        {
            errors["integrationId"] = [$"The integrationId field must be {IntegrationIdMaxLength} characters or fewer."];
        }

        if (string.IsNullOrWhiteSpace(integrationName))
        {
            errors["integrationName"] = ["The integrationName field is required."];
        }
        else if (integrationName.Length > IntegrationNameMaxLength)
        {
            errors["integrationName"] = [$"The integrationName field must be {IntegrationNameMaxLength} characters or fewer."];
        }

        var normalizedScopes = (request.Scopes ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(IntegrationScopes.Normalize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedScopes.Length == 0)
        {
            errors["scopes"] = ["At least one scope is required."];
        }

        if (request.ExpiresAtUtc.HasValue && request.ExpiresAtUtc.Value <= DateTime.UtcNow)
        {
            errors["expiresAtUtc"] = ["The expiresAtUtc field must be in the future."];
        }

        scopes = normalizedScopes;
        return errors.Count == 0;
    }

    private string ResolveCorrelationId()
    {
        var correlationId = HttpContext.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.Trim();
        }

        return HttpContext.TraceIdentifier;
    }

    private ObjectResult ValidationProblem(string code, Dictionary<string, string[]> errors, string? errorClass = null, string? recoveryHint = null)
    {
        var details = new ValidationProblemDetails(errors)
        {
            Type = "https://api.tasktracker.local/problems/validation",
            Title = "Validation failed",
            Status = StatusCodes.Status400BadRequest
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(errorClass))
        {
            details.Extensions["errorClass"] = errorClass;
        }

        if (!string.IsNullOrWhiteSpace(recoveryHint))
        {
            details.Extensions["recovery"] = recoveryHint;
        }

        return StatusCode(StatusCodes.Status400BadRequest, details);
    }

    private ObjectResult UnauthorizedProblem(string code)
    {
        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/authentication-failed",
            Title = "Authentication Failed",
            Status = StatusCodes.Status401Unauthorized
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return StatusCode(StatusCodes.Status401Unauthorized, details);
    }

    private ObjectResult ForbiddenProblem(string code, string? errorClass = null, string? recoveryHint = null)
    {
        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/forbidden",
            Title = "Forbidden",
            Status = StatusCodes.Status403Forbidden
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;
        if (!string.IsNullOrWhiteSpace(errorClass))
        {
            details.Extensions["errorClass"] = errorClass;
        }

        if (!string.IsNullOrWhiteSpace(recoveryHint))
        {
            details.Extensions["recovery"] = recoveryHint;
        }

        return StatusCode(StatusCodes.Status403Forbidden, details);
    }

    private ObjectResult FailureProblem(string code, string errorClass, int statusCode, string recoveryHint)
    {
        var details = new ProblemDetails
        {
            Type = statusCode == StatusCodes.Status503ServiceUnavailable
                ? "https://api.tasktracker.local/problems/service-unavailable"
                : statusCode == StatusCodes.Status409Conflict
                    ? "https://api.tasktracker.local/problems/conflict"
                    : statusCode == StatusCodes.Status404NotFound
                        ? "https://api.tasktracker.local/problems/not-found"
                        : "https://api.tasktracker.local/problems/internal-server-error",
            Title = statusCode == StatusCodes.Status503ServiceUnavailable
                ? "Service Unavailable"
                : statusCode == StatusCodes.Status409Conflict
                    ? "Conflict"
                    : statusCode == StatusCodes.Status404NotFound
                        ? "Not Found"
                        : "Internal Server Error",
            Status = statusCode
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;
        details.Extensions["errorClass"] = errorClass;
        details.Extensions["recovery"] = recoveryHint;

        return StatusCode(statusCode, details);
    }

    private sealed record ClassifiedFailure(
        string ErrorClass,
        string ErrorCode,
        int HttpStatus,
        string RecoveryHint);

    private ObjectResult NotFoundProblem(string code, string detail)
    {
        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/not-found",
            Title = "Not Found",
            Detail = detail,
            Status = StatusCodes.Status404NotFound
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = HttpContext.TraceIdentifier;

        return StatusCode(StatusCodes.Status404NotFound, details);
    }
}
