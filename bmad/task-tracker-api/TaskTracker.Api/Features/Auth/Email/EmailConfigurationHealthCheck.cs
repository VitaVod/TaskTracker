using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace TaskTracker.Api.Features.Auth.Email;

public sealed class EmailConfigurationHealthCheck(
    IOptions<PasswordRecoveryOptions> passwordRecoveryOptions,
    ITransactionalEmailService transactionalEmailService,
    IHostEnvironment hostEnvironment) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var options = passwordRecoveryOptions.Value;
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.FrontendBaseUrl))
        {
            errors.Add("PasswordRecovery:FrontendBaseUrl must be configured.");
        }
        else if (!Uri.TryCreate(options.FrontendBaseUrl, UriKind.Absolute, out var frontendUri)
            || (frontendUri.Scheme != Uri.UriSchemeHttp && frontendUri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("PasswordRecovery:FrontendBaseUrl must be an absolute http or https URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ResetPath))
        {
            errors.Add("PasswordRecovery:ResetPath must be configured.");
        }

        var usingLoggingEmailTransport = transactionalEmailService is LoggingTransactionalEmailService;
        if (usingLoggingEmailTransport && !hostEnvironment.IsDevelopment())
        {
            errors.Add("Transactional email transport is configured to logging-only outside Development.");
        }

        var data = new Dictionary<string, object>
        {
            ["frontendBaseUrlConfigured"] = !string.IsNullOrWhiteSpace(options.FrontendBaseUrl),
            ["resetPathConfigured"] = !string.IsNullOrWhiteSpace(options.ResetPath),
            ["emailTransport"] = transactionalEmailService.GetType().Name,
            ["environment"] = hostEnvironment.EnvironmentName,
            ["usingLoggingTransport"] = usingLoggingEmailTransport,
            ["errorCount"] = errors.Count,
            ["errors"] = errors.ToArray()
        };

        if (errors.Count == 0)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Email configuration is valid.", data));
        }

        return Task.FromResult(HealthCheckResult.Unhealthy("Email configuration is invalid.", data: data));
    }
}
