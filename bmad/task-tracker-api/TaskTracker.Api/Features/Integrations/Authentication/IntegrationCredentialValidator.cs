using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using TaskTracker.Api.Features.Auth.Security;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Features.Integrations.Authentication;

public sealed class IntegrationCredentialValidator(
    TaskTrackerDbContext dbContext,
    IPasswordHasher passwordHasher,
    IHttpContextAccessor httpContextAccessor,
    ILogger<IntegrationCredentialValidator> logger) : IIntegrationCredentialValidator
{
    private static readonly Meter Meter = new("TaskTracker.Api.Integrations", "1.0.0");
    private static readonly Counter<long> AuthAttemptCounter =
        Meter.CreateCounter<long>("integrations.auth.attempt.total");

    public async Task<IntegrationCredentialValidationResult> ValidateAsync(
        string keyId,
        string secret,
        CancellationToken cancellationToken)
    {
        var normalizedKeyId = keyId.Trim();
        var now = DateTime.UtcNow;

        var credential = await dbContext.IntegrationCredentials
            .Include(item => item.Scopes)
            .FirstOrDefaultAsync(item => item.KeyId == normalizedKeyId, cancellationToken);

        if (credential is null)
        {
            TrackAttempt("invalid", string.Empty);
            return new IntegrationCredentialValidationResult(
                IntegrationCredentialValidationStatus.Invalid,
                null,
                []);
        }

        if (credential.Status != IntegrationCredentialStatus.Active || credential.RevokedAtUtc is not null)
        {
            TrackAttempt("revoked", credential.IntegrationId);
            return new IntegrationCredentialValidationResult(
                IntegrationCredentialValidationStatus.Revoked,
                credential,
                []);
        }

        if (credential.ExpiresAtUtc.HasValue && credential.ExpiresAtUtc.Value <= now)
        {
            TrackAttempt("expired", credential.IntegrationId);
            return new IntegrationCredentialValidationResult(
                IntegrationCredentialValidationStatus.Expired,
                credential,
                []);
        }

        if (!passwordHasher.Verify(secret, credential.SecretHash, credential.SecretSalt))
        {
            TrackAttempt("invalid", credential.IntegrationId);
            return new IntegrationCredentialValidationResult(
                IntegrationCredentialValidationStatus.Invalid,
                null,
                []);
        }

        credential.LastUsedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);

        var scopes = credential.Scopes
            .Select(scope => scope.Scope)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        TrackAttempt("success", credential.IntegrationId);
        return new IntegrationCredentialValidationResult(
            IntegrationCredentialValidationStatus.Success,
            credential,
            scopes);
    }

    private void TrackAttempt(string outcome, string integrationId)
    {
        var context = httpContextAccessor.HttpContext;
        var traceId = context?.TraceIdentifier ?? "n/a";
        var correlationId = ResolveCorrelationId(context);

        AuthAttemptCounter.Add(
            1,
            KeyValuePair.Create<string, object?>("outcome", outcome),
            KeyValuePair.Create<string, object?>("integration_id", integrationId));

        logger.LogInformation(
            "Integration authentication evaluated. Outcome: {Outcome}. IntegrationId: {IntegrationId}. CorrelationId: {CorrelationId}. TraceId: {TraceId}",
            outcome,
            integrationId,
            correlationId,
            traceId);
    }

    private static string ResolveCorrelationId(HttpContext? context)
    {
        if (context is null)
        {
            return "n/a";
        }

        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId.Trim();
        }

        return context.TraceIdentifier;
    }
}
