namespace TaskTracker.Api.Infrastructure.Authorization;

public static class AppRoles
{
    public const string User = "User";
    public const string Admin = "Admin";
    public const string Support = "Support";

    public static readonly string[] All = [User, Admin, Support];
}
