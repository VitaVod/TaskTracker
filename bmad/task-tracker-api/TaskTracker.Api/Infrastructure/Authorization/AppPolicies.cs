namespace TaskTracker.Api.Infrastructure.Authorization;

public static class AppPolicies
{
    public const string AuthenticatedUser = "AuthenticatedUser";
    public const string AdminOnly = "AdminOnly";
    public const string SupportOnly = "SupportOnly";
    public const string AccountOwnerOrPrivileged = "AccountOwnerOrPrivileged";
}
