using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace TaskTracker.Api.Features.Integrations.Authentication;

public sealed class IntegrationAuthenticationHandler(
    IOptionsMonitor<IntegrationAuthenticationOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IIntegrationCredentialValidator validator) : AuthenticationHandler<IntegrationAuthenticationOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var keyId = Request.Headers[IntegrationAuthenticationDefaults.KeyIdHeader].FirstOrDefault();
        var secret = Request.Headers[IntegrationAuthenticationDefaults.SecretHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(keyId) || string.IsNullOrWhiteSpace(secret))
        {
            return Fail("auth.integration.invalid", "missing");
        }

        var validation = await validator.ValidateAsync(keyId, secret, Context.RequestAborted);

        if (validation.Status != IntegrationCredentialValidationStatus.Success || validation.Credential is null)
        {
            return validation.Status switch
            {
                IntegrationCredentialValidationStatus.Revoked => Fail("auth.integration.revoked", "revoked"),
                IntegrationCredentialValidationStatus.Expired => Fail("auth.integration.expired", "expired"),
                _ => Fail("auth.integration.invalid", "invalid")
            };
        }

        var credential = validation.Credential;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, credential.OwnerUserId.ToString()),
            new("integration_credential_id", credential.Id.ToString()),
            new("integration_key_id", credential.KeyId),
            new("integration_id", credential.IntegrationId),
            new("integration_name", credential.IntegrationName)
        };

        foreach (var scope in validation.Scopes)
        {
            claims.Add(new Claim(IntegrationAuthenticationDefaults.ScopeClaimType, scope));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Response.HasStarted)
        {
            return;
        }

        var code = Context.Items.TryGetValue("integration.auth.code", out var value)
            ? value?.ToString() ?? "auth.integration.invalid"
            : "auth.integration.invalid";

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/problem+json";

        var details = new ProblemDetails
        {
            Type = "https://api.tasktracker.local/problems/authentication-failed",
            Title = "Authentication Failed",
            Status = StatusCodes.Status401Unauthorized
        };

        details.Extensions["code"] = code;
        details.Extensions["traceId"] = Context.TraceIdentifier;

        await Response.WriteAsJsonAsync(
            details,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: Context.RequestAborted);
    }

    private AuthenticateResult Fail(string code, string outcome)
    {
        Context.Items["integration.auth.code"] = code;
        Context.Items["integration.auth.outcome"] = outcome;
        return AuthenticateResult.Fail(code);
    }
}
