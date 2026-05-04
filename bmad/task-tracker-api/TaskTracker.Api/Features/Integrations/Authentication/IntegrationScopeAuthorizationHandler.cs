using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Authorization;

namespace TaskTracker.Api.Features.Integrations.Authentication;

public sealed class IntegrationScopeAuthorizationHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<IntegrationScopeAuthorizationHandler> logger) : AuthorizationHandler<IntegrationScopeRequirement>
{
    private static readonly Meter Meter = new("TaskTracker.Api.Integrations", "1.0.0");
    private static readonly Counter<long> ForbiddenScopeCounter =
        Meter.CreateCounter<long>("integrations.auth.forbidden.scope.total");

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        IntegrationScopeRequirement requirement)
    {
        var hasScope = context.User.Claims
            .Where(claim => claim.Type == IntegrationAuthenticationDefaults.ScopeClaimType)
            .Any(claim => string.Equals(
                claim.Value,
                requirement.RequiredScope,
                StringComparison.Ordinal));

        if (hasScope)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var traceId = httpContextAccessor.HttpContext?.TraceIdentifier ?? "n/a";
        var correlationId = ResolveCorrelationId(httpContextAccessor.HttpContext);
        var integrationId = context.User.Claims
            .FirstOrDefault(claim => claim.Type == "integration_id")?.Value ?? "unknown";

        ForbiddenScopeCounter.Add(
            1,
            KeyValuePair.Create<string, object?>("scope", requirement.RequiredScope));

        logger.LogWarning(
            "Integration scope denied. IntegrationId: {IntegrationId}. RequiredScope: {RequiredScope}. CorrelationId: {CorrelationId}. TraceId: {TraceId}",
            integrationId,
            requirement.RequiredScope,
            correlationId,
            traceId);

        return Task.CompletedTask;
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
