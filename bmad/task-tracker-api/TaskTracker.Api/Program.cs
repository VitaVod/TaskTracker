using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TaskTracker.Api.Features.Account.Repositories;
using TaskTracker.Api.Features.Account.Validation;
using TaskTracker.Api.Features.Auth.Email;
using TaskTracker.Api.Features.Auth.Repositories;
using TaskTracker.Api.Features.Auth.Security;
using TaskTracker.Api.Features.Auth.Tokens;
using TaskTracker.Api.Features.Integrations.Authentication;
using TaskTracker.Api.Features.Integrations.Services;
using TaskTracker.Api.Features.Leaderboards.Repositories;
using TaskTracker.Api.Features.Notifications.AccountEvents;
using TaskTracker.Api.Features.Notifications.Reminders;
using TaskTracker.Api.Features.Notifications.Validation;
using TaskTracker.Api.Features.Operations.Auditing;
using TaskTracker.Api.Features.Progress.Repositories;
using TaskTracker.Api.Features.Progress.Configuration;
using TaskTracker.Api.Features.SharedViews.Caching;
using TaskTracker.Api.Features.Statistics.Repositories;
using TaskTracker.Api.Features.Tasks.Repositories;
using TaskTracker.Api.Features.Tasks.Streaks;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddDbContext<TaskTrackerDbContext>(options =>
{
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("TaskTrackerDb"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(5));
});

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<PasswordRecoveryOptions>(builder.Configuration.GetSection(PasswordRecoveryOptions.SectionName));
builder.Services.Configure<SharedViewCacheOptions>(builder.Configuration.GetSection(SharedViewCacheOptions.SectionName));
builder.Services.Configure<ProgressionLevelOptions>(builder.Configuration.GetSection(ProgressionLevelOptions.SectionName));
builder.Services.AddHealthChecks()
    .AddCheck<EmailConfigurationHealthCheck>("email_configuration", tags: ["startup", "ops"]);

var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "tasktracker:";
    });
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<ITransactionalEmailService, LoggingTransactionalEmailService>();
builder.Services.AddScoped<IIntegrationCredentialService, IntegrationCredentialService>();
builder.Services.AddScoped<IIntegrationCredentialValidator, IntegrationCredentialValidator>();
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IProgressRepository, ProgressRepository>();
builder.Services.AddScoped<ILeaderboardRepository, LeaderboardRepository>();
builder.Services.AddScoped<IGlobalStatisticsRepository, GlobalStatisticsRepository>();
builder.Services.AddScoped<IReminderProcessingService, ReminderProcessingService>();
builder.Services.AddScoped<IAccountEventNotificationService, AccountEventNotificationService>();
builder.Services.AddScoped<IPrivilegedAuditWriter, PrivilegedAuditWriter>();
builder.Services.AddSingleton<ISharedViewCacheCoordinator, SharedViewCacheCoordinator>();
builder.Services.AddSingleton<IStreakRuleEngine, StreakRuleEngine>();
builder.Services.AddScoped<IAuthorizationHandler, RouteUserOwnershipHandler>();
builder.Services.AddScoped<IAuthorizationHandler, IntegrationScopeAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, TraceableAuthorizationMiddlewareResultHandler>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IAccountUpdateValidator, AccountUpdateValidator>();
builder.Services.AddSingleton<INotificationPreferencesValidator, NotificationPreferencesValidator>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT configuration is missing.");

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            NameClaimType = "sub",
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var tokenType = context.Principal?.FindFirst("token_type")?.Value;
                if (!string.Equals(tokenType, "access", StringComparison.Ordinal))
                {
                    context.Fail("Only access tokens can be used to call protected endpoints.");
                    return;
                }

                // Logout is exempt from revocation check so it remains idempotent —
                // a caller whose session was already revoked can still hit logout.
                if (context.HttpContext.Request.Path.StartsWithSegments("/api/v1/auth/logout"))
                {
                    return;
                }

                var sessionIdClaim = context.Principal?.FindFirst("session_id")?.Value;
                if (sessionIdClaim is null || !Guid.TryParse(sessionIdClaim, out var sessionId))
                {
                    context.Fail("Access token does not contain a valid session identifier.");
                    return;
                }

                var authRepository = context.HttpContext.RequestServices
                    .GetRequiredService<IAuthRepository>();

                var session = await authRepository.FindSessionAsync(
                    sessionId, context.HttpContext.RequestAborted);

                if (session is null || session.RevokedAtUtc is not null)
                {
                    context.Fail("Session has been revoked or does not exist.");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";

                var details = new Microsoft.AspNetCore.Mvc.ProblemDetails
                {
                    Type = "https://api.tasktracker.local/problems/authentication-failed",
                    Title = "Authentication Failed",
                    Status = StatusCodes.Status401Unauthorized
                };

                details.Extensions["code"] = "auth.session.invalid";
                details.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                await context.Response.WriteAsJsonAsync(details, cancellationToken: context.HttpContext.RequestAborted);
            }
        };
    })
    .AddScheme<IntegrationAuthenticationOptions, IntegrationAuthenticationHandler>(
        IntegrationAuthenticationDefaults.Scheme,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AppPolicies.AuthenticatedUser, policy =>
        policy.RequireRole(AppRoles.All));

    options.AddPolicy(AppPolicies.AdminOnly, policy =>
        policy.RequireRole(AppRoles.Admin));

    options.AddPolicy(AppPolicies.SupportOnly, policy =>
        policy.RequireRole(AppRoles.Support));

    options.AddPolicy(AppPolicies.AccountOwnerOrPrivileged, policy =>
    {
        policy.RequireRole(AppRoles.All);
        policy.Requirements.Add(new OwnershipRequirement("userId", AppRoles.Admin, AppRoles.Support));
    });

    options.AddPolicy(AppPolicies.IntegrationAuthenticated, policy =>
    {
        policy.AddAuthenticationSchemes(IntegrationAuthenticationDefaults.Scheme);
        policy.RequireAuthenticatedUser();
    });

    options.AddPolicy(AppPolicies.IntegrationTaskCreateSync, policy =>
    {
        policy.AddAuthenticationSchemes(IntegrationAuthenticationDefaults.Scheme);
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new IntegrationScopeRequirement(IntegrationScopes.TasksCreateSync));
    });
});

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://api.tasktracker.local/problems/validation",
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest
            };

            details.Extensions["code"] = "validation.request.invalid";
            details.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

            return new BadRequestObjectResult(details);
        };
    });
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

using (var startupScope = app.Services.CreateScope())
{
    var startupHealthChecks = startupScope.ServiceProvider.GetRequiredService<HealthCheckService>();
    var startupLogger = startupScope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("StartupHealthChecks");
    var startupHealthReport = await startupHealthChecks.CheckHealthAsync(registration => registration.Tags.Contains("startup"));

    if (startupHealthReport.Status == HealthStatus.Healthy)
    {
        startupLogger.LogInformation("Startup health checks completed with healthy status.");
    }
    else
    {
        startupLogger.LogError("Startup health checks reported status {Status}.", startupHealthReport.Status);
    }
}

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;

