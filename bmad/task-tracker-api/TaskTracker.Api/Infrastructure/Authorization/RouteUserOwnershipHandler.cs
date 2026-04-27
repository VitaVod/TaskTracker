using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TaskTracker.Api.Infrastructure.Authorization;

public sealed class RouteUserOwnershipHandler : AuthorizationHandler<OwnershipRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, OwnershipRequirement requirement)
    {
        if (requirement.PrivilegedRoles.Any(context.User.IsInRole))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var currentUserIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirstValue("sub");

        if (!Guid.TryParse(currentUserIdRaw, out var currentUserId))
        {
            return Task.CompletedTask;
        }

        var routeValue = ResolveRouteValue(context.Resource, requirement.RouteParameterName);
        if (!Guid.TryParse(routeValue, out var routeUserId))
        {
            return Task.CompletedTask;
        }

        if (routeUserId == currentUserId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }

    private static string? ResolveRouteValue(object? resource, string routeParameterName)
    {
        return resource switch
        {
            HttpContext httpContext => httpContext.GetRouteValue(routeParameterName)?.ToString(),
            AuthorizationFilterContext filterContext => filterContext.RouteData.Values[routeParameterName]?.ToString(),
            _ => null
        };
    }
}
