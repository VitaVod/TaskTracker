using Microsoft.AspNetCore.Authorization;

namespace TaskTracker.Api.Infrastructure.Authorization;

public sealed class OwnershipRequirement(string routeParameterName, params string[] privilegedRoles) : IAuthorizationRequirement
{
    public string RouteParameterName { get; } = routeParameterName;

    public IReadOnlyCollection<string> PrivilegedRoles { get; } = privilegedRoles;
}
