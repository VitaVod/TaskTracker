namespace TaskTracker.Api.Features.Integrations.Authentication;

public static class IntegrationAuthenticationDefaults
{
    public const string Scheme = "IntegrationCredential";
    public const string KeyIdHeader = "X-Integration-Key-Id";
    public const string SecretHeader = "X-Integration-Secret";
    public const string ScopeClaimType = "integration_scope";
}
