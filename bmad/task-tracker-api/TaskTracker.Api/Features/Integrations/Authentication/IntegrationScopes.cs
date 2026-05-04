namespace TaskTracker.Api.Features.Integrations.Authentication;

public static class IntegrationScopes
{
    public const string TasksCreateSync = "tasks:create-sync";

    public static string Normalize(string scope) => scope.Trim().ToLowerInvariant();
}
