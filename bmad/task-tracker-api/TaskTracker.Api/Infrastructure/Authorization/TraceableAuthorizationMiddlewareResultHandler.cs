using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Api.Features.Integrations.Authentication;

namespace TaskTracker.Api.Infrastructure.Authorization;

public sealed class TraceableAuthorizationMiddlewareResultHandler(
    ILogger<TraceableAuthorizationMiddlewareResultHandler> logger) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var code = ResolveForbiddenCode(authorizeResult.AuthorizationFailure);
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub")
            ?? "anonymous";

        logger.LogWarning(
            "Authorization denied. User: {UserId}. Path: {Path}. Method: {Method}. Code: {Code}. TraceId: {TraceId}",
            userId,
            context.Request.Path.Value,
            context.Request.Method,
            code,
            context.TraceIdentifier);

        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/forbidden",
            Title = "Forbidden",
            Status = StatusCodes.Status403Forbidden
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(
            details,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }

    private static string ResolveForbiddenCode(AuthorizationFailure? failure)
    {
        if (failure?.FailedRequirements.Any(req => req is IntegrationScopeRequirement) == true)
        {
            return "auth.integration.scope.denied";
        }

        if (failure?.FailedRequirements.Any(req => req is OwnershipRequirement) == true)
        {
            return "authz.ownership.denied";
        }

        return "authz.access.denied";
    }
}
