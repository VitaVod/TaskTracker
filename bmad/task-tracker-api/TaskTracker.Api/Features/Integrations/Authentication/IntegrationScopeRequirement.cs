using Microsoft.AspNetCore.Authorization;

namespace TaskTracker.Api.Features.Integrations.Authentication;

public sealed class IntegrationScopeRequirement(string requiredScope) : IAuthorizationRequirement
{
    public string RequiredScope { get; } = IntegrationScopes.Normalize(requiredScope);
}
