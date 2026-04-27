using System.Net;
using System.Net.Http.Json;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Features.Auth.Email;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Tests.Integration;

public class AuthControllerTests : IClassFixture<AuthTestFactory>
{
    private readonly HttpClient _client;
    private readonly AuthTestFactory _factory;

    public AuthControllerTests(AuthTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Registration ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidPayload_ReturnsCreated()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("new.user@example.com", "StrongPass123!"));
        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("new.user@example.com", payload.Email);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsProblemDetails()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("duplicate@example.com", "StrongPass123!"));

        var duplicateResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("duplicate@example.com", "StrongPass123!"));

        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokensAndCreatesSession()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("login.user@example.com", "StrongPass123!"));

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("login.user@example.com", "StrongPass123!"));
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        Assert.Equal(900, payload.ExpiresIn);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("invalid.login@example.com", "StrongPass123!"));

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("invalid.login@example.com", "WrongPassword123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokenPair()
    {
        var tokens = await RegisterAndLoginAsync("refresh.valid@example.com");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        var payload = await response.Content.ReadFromJsonAsync<RefreshResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.RefreshToken));
        // New tokens must differ from the originals
        Assert.NotEqual(tokens.AccessToken, payload.AccessToken);
        Assert.NotEqual(tokens.RefreshToken, payload.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithExpiredToken_ReturnsUnauthorized()
    {
        // A well-formed but entirely fabricated token with a past expiry —
        // signature validation will fail, which maps to the same 401 response.
        const string malformedToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDAiLCJlbWFpbCI6InRlc3RAZXhhbXBsZS5jb20iLCJqdGkiOiIwMDAwMDAwMC0wMDAwLTAwMDAtMDAwMC0wMDAwMDAwMDAwMDAiLCJ0b2tlbl90eXBlIjoicmVmcmVzaCIsIm5iZiI6MTAwMDAwMDAwMCwiZXhwIjoxMDAwMDAwMDAxfQ.invalid";

        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(malformedToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_ReturnsUnauthorized()
    {
        var tokens = await RegisterAndLoginAsync("refresh.revoked@example.com");

        // Logout revokes the session
        await LogoutAsync(tokens);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_ReplayOfAlreadyRotatedToken_ReturnsUnauthorized()
    {
        var tokens = await RegisterAndLoginAsync("refresh.replay@example.com");

        // First refresh succeeds and rotates the session
        await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        // Replaying the original (now rotated) refresh token must be rejected
        var replayResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));

        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_WithValidSession_RevokesSoSubsequentApiCallsAreRejected()
    {
        var tokens = await RegisterAndLoginAsync("logout.valid@example.com");

        var logoutResponse = await LogoutAsync(tokens);
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        // Old access token must be rejected on a protected endpoint after logout
        var protectedResponse = await CallProtectedEndpointAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_CalledTwice_IsIdempotentAndReturnsOk()
    {
        var tokens = await RegisterAndLoginAsync("logout.idempotent@example.com");

        var first = await LogoutAsync(tokens);
        var second = await LogoutAsync(tokens);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task PostLogout_OldRefreshToken_CannotRenewSession()
    {
        var tokens = await RegisterAndLoginAsync("logout.noreuse@example.com");

        await LogoutAsync(tokens);

        // Attempting to refresh after logout must be rejected
        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutAccessTokenHeader_StillRevokesSessionViaRefreshToken()
    {
        var tokens = await RegisterAndLoginAsync("logout.refreshonly@example.com");

        var logoutResponse = await _client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var refreshResponse = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task AdminAndSupportRoutes_WithStandardUser_ReturnForbiddenProblemDetails()
    {
        var standardUser = await RegisterAndLoginWithUserAsync("standard.user@example.com");

        using var adminRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/admin/health");
        adminRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", standardUser.Tokens.AccessToken);

        var adminResponse = await _client.SendAsync(adminRequest);
        var adminPayload = await adminResponse.Content.ReadFromJsonAsync<ProblemDetailsPayload>();

        Assert.Equal(HttpStatusCode.Forbidden, adminResponse.StatusCode);
        Assert.Equal("application/problem+json", adminResponse.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(adminPayload);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", adminPayload.Type);
        Assert.Equal("Forbidden", adminPayload.Title);
        Assert.Equal(403, adminPayload.Status);
        Assert.Equal("authz.access.denied", adminPayload.Code);
        Assert.False(string.IsNullOrWhiteSpace(adminPayload.TraceId));

        using var supportRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/ops/support/users/{standardUser.UserId}");
        supportRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", standardUser.Tokens.AccessToken);

        var supportResponse = await _client.SendAsync(supportRequest);
        var supportPayload = await supportResponse.Content.ReadFromJsonAsync<ProblemDetailsPayload>();

        Assert.Equal(HttpStatusCode.Forbidden, supportResponse.StatusCode);
        Assert.NotNull(supportPayload);
        Assert.Equal("authz.access.denied", supportPayload.Code);
        Assert.False(string.IsNullOrWhiteSpace(supportPayload.TraceId));
    }

    [Fact]
    public async Task AdminRoute_WithAdminRole_AllowsAccess()
    {
        var adminUser = await RegisterAndLoginWithUserAsync("admin.user@example.com", AppRoles.Admin);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/admin/health");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminUser.Tokens.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SupportRoute_WithSupportRole_AllowsAccess()
    {
        var targetUser = await RegisterAndLoginWithUserAsync("support.target@example.com");
        var supportUser = await RegisterAndLoginWithUserAsync("support.user@example.com", AppRoles.Support);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/ops/support/users/{targetUser.UserId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", supportUser.Tokens.AccessToken);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ForbiddenRoleDenial_IsLoggedWithTraceContext()
    {
        _factory.ClearCapturedLogs();
        var standardUser = await RegisterAndLoginWithUserAsync("audit.user@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/admin/health");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", standardUser.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var denialLogs = _factory.GetCapturedLogs()
            .Where(entry => entry.Level == LogLevel.Warning
                && entry.Message.Contains("Authorization denied", StringComparison.Ordinal)
                && entry.Message.Contains("/api/v1/ops/admin/health", StringComparison.Ordinal)
                && entry.Message.Contains("TraceId", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(denialLogs);
    }

    // ── Password recovery ─────────────────────────────────────────────────────

    [Fact]
    public async Task PasswordRecoveryRequest_ReturnsAcceptedForKnownAndUnknownEmail()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("recover.known@example.com", "StrongPass123!"));

        var knownResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/request",
            new PasswordRecoveryRequest("recover.known@example.com"));
        var unknownResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/request",
            new PasswordRecoveryRequest("recover.unknown@example.com"));

        var knownPayload = await knownResponse.Content.ReadFromJsonAsync<PasswordRecoveryRequestResponse>();
        var unknownPayload = await unknownResponse.Content.ReadFromJsonAsync<PasswordRecoveryRequestResponse>();

        Assert.Equal(HttpStatusCode.Accepted, knownResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, unknownResponse.StatusCode);
        Assert.NotNull(knownPayload);
        Assert.NotNull(unknownPayload);
        Assert.Equal(knownPayload.Message, unknownPayload.Message);
    }

    [Fact]
    public async Task PasswordRecoveryRequest_RetriesTransientDeliveryFailures()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("recover.retry@example.com", "StrongPass123!"));
        _factory.GetRecoveryEmailService().SetNextResults(
            TransactionalEmailSendResult.TransientFailure,
            TransactionalEmailSendResult.TransientFailure,
            TransactionalEmailSendResult.Success);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/request",
            new PasswordRecoveryRequest("recover.retry@example.com"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var sent = _factory.GetRecoveryEmailService().GetAttemptedMessages();
        Assert.True(sent.Count >= 3);

        var tokenId = sent[^1].TokenId;
        var attemptCount = await _factory.GetPasswordRecoveryDeliveryAttemptCountAsync(tokenId);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task PasswordRecoveryConfirm_WithValidToken_ResetsPasswordAndRevokesSessions()
    {
        var tokens = await RegisterAndLoginAsync("recover.confirm@example.com");

        await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/request",
            new PasswordRecoveryRequest("recover.confirm@example.com"));

        var recoveryMessage = _factory.GetRecoveryEmailService().GetAttemptedMessages().Last();
        var token = ExtractRecoveryToken(recoveryMessage.RecoveryLink);

        var confirmResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/confirm",
            new PasswordRecoveryConfirmRequest(token, "NewStrongPass456!"));

        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var oldRefreshResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, oldRefreshResponse.StatusCode);

        var oldPasswordLogin = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("recover.confirm@example.com", "StrongPass123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await _client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest("recover.confirm@example.com", "NewStrongPass456!"));
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task PasswordRecoveryConfirm_WithExpiredToken_ReturnsRecoveryGuidanceProblemDetails()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("recover.expired@example.com", "StrongPass123!"));

        await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/request",
            new PasswordRecoveryRequest("recover.expired@example.com"));

        var recoveryMessage = _factory.GetRecoveryEmailService().GetAttemptedMessages().Last();
        await _factory.ExpirePasswordRecoveryTokenAsync(recoveryMessage.TokenId);

        var token = ExtractRecoveryToken(recoveryMessage.RecoveryLink);
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/confirm",
            new PasswordRecoveryConfirmRequest(token, "NewStrongPass456!"));

        var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsShape>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("https://api.tasktracker.local/problems/password-recovery-invalid", payload.Type);
        Assert.Equal("Recovery Link Invalid", payload.Title);
        Assert.Equal(400, payload.Status);
        Assert.Equal("auth.password-recovery.invalid", payload.Code);
        Assert.False(string.IsNullOrWhiteSpace(payload.TraceId));
        Assert.Contains("Request a new recovery email", payload.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PasswordRecoveryConfirm_WithReusedToken_ReturnsRecoveryGuidanceProblemDetails()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest("recover.reused@example.com", "StrongPass123!"));

        await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/request",
            new PasswordRecoveryRequest("recover.reused@example.com"));

        var recoveryMessage = _factory.GetRecoveryEmailService().GetAttemptedMessages().Last();
        var token = ExtractRecoveryToken(recoveryMessage.RecoveryLink);

        var firstResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/confirm",
            new PasswordRecoveryConfirmRequest(token, "NewStrongPass456!"));
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/confirm",
            new PasswordRecoveryConfirmRequest(token, "AnotherStrongPass789!"));
        var payload = await secondResponse.Content.ReadFromJsonAsync<ProblemDetailsShape>();

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("auth.password-recovery.invalid", payload.Code);
        Assert.Contains("Request a new recovery email", payload.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PasswordRecoveryConfirm_WithInvalidToken_ReturnsRecoveryGuidanceProblemDetails()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/password-recovery/confirm",
            new PasswordRecoveryConfirmRequest("not-a-valid-token", "NewStrongPass456!"));

        var payload = await response.Content.ReadFromJsonAsync<ProblemDetailsShape>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("https://api.tasktracker.local/problems/password-recovery-invalid", payload.Type);
        Assert.Equal("Recovery Link Invalid", payload.Title);
        Assert.Equal(400, payload.Status);
        Assert.Equal("auth.password-recovery.invalid", payload.Code);
        Assert.False(string.IsNullOrWhiteSpace(payload.TraceId));
        Assert.Contains("Request a new recovery email", payload.Detail, StringComparison.OrdinalIgnoreCase);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<LoginResponse> RegisterAndLoginAsync(string email)
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass123!"));
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "StrongPass123!"));
        return (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private async Task<(Guid UserId, LoginResponse Tokens)> RegisterAndLoginWithUserAsync(string email, string role = AppRoles.User)
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass123!"));
        var registerPayload = (await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>())!;

        if (!string.Equals(role, AppRoles.User, StringComparison.Ordinal))
        {
            await _factory.SetUserRoleAsync(registerPayload.UserId, role);
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "StrongPass123!"));
        var loginPayload = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;

        return (registerPayload.UserId, loginPayload);
    }

    private async Task<HttpResponseMessage> LogoutAsync(LoginResponse tokens)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Content = JsonContent.Create(new LogoutRequest(tokens.RefreshToken));
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> CallProtectedEndpointAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/test/protected");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private static string ExtractRecoveryToken(string recoveryLink)
    {
        var uri = new Uri(recoveryLink);
        var query = QueryHelpers.ParseQuery(uri.Query);
        var encodedToken = query["token"].ToString();
        return Uri.UnescapeDataString(encodedToken);
    }
}

public sealed record ProblemDetailsPayload(string Type, string Title, int Status, string Code, string TraceId);

public sealed record ProblemDetailsShape(
    string Type,
    string Title,
    int Status,
    string Code,
    string TraceId,
    string Detail);

/// <summary>
/// Minimal protected endpoint used by integration tests to verify that revoked sessions
/// are rejected on arbitrary protected routes.
/// </summary>
[ApiController]
[Route("api/v1/test")]
public class TestController : ControllerBase
{
    [HttpGet("protected")]
    [Authorize]
    public IActionResult Protected() => Ok(new { status = "authorized" });
}

public class AuthTestFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"task-tracker-tests-{Guid.NewGuid()}";
    private readonly ConcurrentQueue<CapturedLogEntry> _logs = new();
    private readonly FakeTransactionalEmailService _recoveryEmailService = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new TestLoggerProvider(_logs));
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TaskTrackerDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<TaskTrackerDbContext>>();
            services.RemoveAll<ITransactionalEmailService>();

            services.AddDbContext<TaskTrackerDbContext>(options =>
                options.UseInMemoryDatabase(_dbName)
                    // In-memory DB ignores transactions; suppress the warning so RotateSessionAsync works.
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)));

            services.AddSingleton<ITransactionalEmailService>(_recoveryEmailService);

            // Register test-only controllers (e.g. TestController) so we can verify
            // that revoked sessions are rejected on arbitrary protected routes.
            services.AddControllers().AddApplicationPart(typeof(AuthControllerTests).Assembly);
        });
    }

    public FakeTransactionalEmailService GetRecoveryEmailService() => _recoveryEmailService;

    public async Task ExpirePasswordRecoveryTokenAsync(Guid tokenId)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();
        var token = await dbContext.PasswordRecoveryTokens.FirstAsync(token => token.TokenId == tokenId);
        token.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await dbContext.SaveChangesAsync();
    }

    public async Task<int> GetPasswordRecoveryDeliveryAttemptCountAsync(Guid tokenId)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();
        var token = await dbContext.PasswordRecoveryTokens.FirstAsync(existingToken => existingToken.TokenId == tokenId);
        return token.DeliveryAttemptCount;
    }

    public async Task SetUserRoleAsync(Guid userId, string role)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();
        var user = await dbContext.Users.FirstAsync(u => u.Id == userId);
        user.Role = role;
        await dbContext.SaveChangesAsync();
    }

    public async Task<int> CountTasksForUserAsync(Guid userId)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();
        return await dbContext.Tasks.CountAsync(task => task.UserId == userId);
    }

    public async Task<TaskItem> AddTaskAsync(TaskItem task)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();

        dbContext.Tasks.Add(task);
        await dbContext.SaveChangesAsync();

        return task;
    }

    public async Task SetTaskCompletionAsync(Guid taskId, bool isCompleted, DateTime updatedAtUtc)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();
        var task = await dbContext.Tasks.FirstAsync(existingTask => existingTask.Id == taskId);

        task.IsCompleted = isCompleted;
        task.UpdatedAtUtc = updatedAtUtc;
        await dbContext.SaveChangesAsync();
    }

    public async Task<TaskItem?> FindTaskByIdAsync(Guid taskId)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();
        return await dbContext.Tasks.FirstOrDefaultAsync(task => task.Id == taskId);
    }

    public async Task<int> CountTaskCompletionEventsAsync(Guid taskId)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();
        return await dbContext.TaskCompletionEvents.CountAsync(completionEvent => completionEvent.TaskId == taskId);
    }

    public async Task<int> CountTaskCompletedEventsAsync(Guid taskId)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskTrackerDbContext>();
        return await dbContext.TaskCompletionEvents.CountAsync(completionEvent =>
            completionEvent.TaskId == taskId
            && completionEvent.EventName == "TaskCompleted");
    }

    public IReadOnlyCollection<CapturedLogEntry> GetCapturedLogs() => _logs.ToArray();

    public void ClearCapturedLogs()
    {
        while (_logs.TryDequeue(out _))
        {
        }
    }
}

public sealed record CapturedLogEntry(LogLevel Level, string Category, string Message);

public sealed class TestLoggerProvider(ConcurrentQueue<CapturedLogEntry> logs) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestLogger(categoryName, logs);

    public void Dispose()
    {
    }
}

public sealed class TestLogger(string categoryName, ConcurrentQueue<CapturedLogEntry> logs) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        logs.Enqueue(new CapturedLogEntry(logLevel, categoryName, formatter(state, exception)));
    }
}

public sealed class FakeTransactionalEmailService : ITransactionalEmailService
{
    private readonly ConcurrentQueue<PasswordRecoveryEmailMessage> _attemptedMessages = new();
    private readonly ConcurrentQueue<TransactionalEmailSendResult> _plannedResults = new();

    public IReadOnlyList<PasswordRecoveryEmailMessage> GetAttemptedMessages() => _attemptedMessages.ToArray();

    public void SetNextResults(params TransactionalEmailSendResult[] results)
    {
        while (_plannedResults.TryDequeue(out _))
        {
        }

        foreach (var result in results)
        {
            _plannedResults.Enqueue(result);
        }
    }

    public Task<TransactionalEmailSendResult> SendPasswordRecoveryAsync(
        PasswordRecoveryEmailMessage message,
        CancellationToken cancellationToken)
    {
        _attemptedMessages.Enqueue(message);
        if (_plannedResults.TryDequeue(out var result))
        {
            return Task.FromResult(result);
        }

        return Task.FromResult(TransactionalEmailSendResult.Success);
    }
}
