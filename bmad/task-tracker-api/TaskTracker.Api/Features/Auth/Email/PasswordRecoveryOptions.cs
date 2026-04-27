namespace TaskTracker.Api.Features.Auth.Email;

public class PasswordRecoveryOptions
{
    public const string SectionName = "PasswordRecovery";

    // Public web-app base URL where users complete reset, e.g. https://app.tasktracker.local
    public string FrontendBaseUrl { get; set; } = string.Empty;

    // Relative reset route used by the frontend.
    public string ResetPath { get; set; } = "/reset-password";
}
