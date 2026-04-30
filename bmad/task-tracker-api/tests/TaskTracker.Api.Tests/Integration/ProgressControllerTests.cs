using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Tests.Integration;

public class ProgressControllerTests : IClassFixture<AuthTestFactory>
{
    private readonly HttpClient _client;
    private readonly AuthTestFactory _factory;

    public ProgressControllerTests(AuthTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetXpSummary_WithAuthenticatedUser_ReturnsOwnedTotalsOnly()
    {
        var caller = await RegisterAndLoginWithUserAsync("progress.xp.caller@example.com");
        var otherUser = await RegisterAndLoginWithUserAsync("progress.xp.other@example.com");

        var callerTask = await SeedTaskAsync(caller.UserId, "Caller progression task");
        var otherTask = await SeedTaskAsync(otherUser.UserId, "Other progression task");

        await SeedCompletionAndXpAsync(caller.UserId, callerTask.Id, DateTime.UtcNow.AddDays(-2), 10);
        await SeedCompletionAndXpAsync(caller.UserId, callerTask.Id, DateTime.UtcNow.AddDays(-1), 25);
        await SeedCompletionAndXpAsync(otherUser.UserId, otherTask.Id, DateTime.UtcNow.AddDays(-1), 100);

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/progress/xp-summary?userId={otherUser.UserId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(35, payload.GetProperty("totalXp").GetInt32());
        Assert.Equal(2, payload.GetProperty("ledgerEntryCount").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("lastGrantedAtUtc").GetString()));
    }

    [Fact]
    public async Task GetStreak_WithPersistedSnapshot_ReturnsSnapshotForAuthenticatedUser()
    {
        var caller = await RegisterAndLoginWithUserAsync("progress.streak.snapshot@example.com");
        var evaluatedAtUtc = DateTime.UtcNow.AddMinutes(-20);

        await _factory.UpsertStreakSnapshotAsync(new UserStreakSnapshot
        {
            OwnerId = caller.UserId,
            Outcome = TaskStreakOutcome.Continue,
            CurrentStreakDays = 4,
            LongestStreakDays = 9,
            TimeZoneId = "UTC",
            EvaluationWindowStartUtc = evaluatedAtUtc.AddHours(-24),
            EvaluationWindowEndUtc = evaluatedAtUtc,
            LastEvaluatedEventId = Guid.NewGuid(),
            LastEvaluationTraceId = "trace-progress-streak",
            LastEvaluatedAtUtc = evaluatedAtUtc
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/progress/streak");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("continue", payload.GetProperty("outcome").GetString());
        Assert.Equal(4, payload.GetProperty("currentStreakDays").GetInt32());
        Assert.Equal(9, payload.GetProperty("longestStreakDays").GetInt32());
        Assert.Equal("UTC", payload.GetProperty("timeZoneId").GetString());
        Assert.Equal(evaluatedAtUtc, payload.GetProperty("lastEvaluatedAtUtc").GetDateTime());
    }

    [Fact]
    public async Task GetTrend_DailyWindow_ReturnsDeterministicBoundedSnapshot()
    {
        var caller = await RegisterAndLoginWithUserAsync("progress.trend.daily@example.com");
        var otherUser = await RegisterAndLoginWithUserAsync("progress.trend.daily.other@example.com");

        var callerTask = await SeedTaskAsync(caller.UserId, "Trend caller task");
        var otherTask = await SeedTaskAsync(otherUser.UserId, "Trend other task");

        await SeedCompletionAndXpAsync(caller.UserId, callerTask.Id, DateTime.UtcNow.AddDays(-1), 10);
        await SeedCompletionAndXpAsync(caller.UserId, callerTask.Id, DateTime.UtcNow.AddDays(-3), 20);
        await SeedCompletionAndXpAsync(otherUser.UserId, otherTask.Id, DateTime.UtcNow.AddDays(-1), 200);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/progress/trend?granularity=daily&windowDays=7");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("daily", payload.GetProperty("granularity").GetString());
        Assert.Equal(7, payload.GetProperty("windowDays").GetInt32());

        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(7, items.Length);

        var completedTotal = items.Sum(item => item.GetProperty("completedTaskCount").GetInt32());
        var xpTotal = items.Sum(item => item.GetProperty("xpGranted").GetInt32());

        Assert.Equal(2, completedTotal);
        Assert.Equal(30, xpTotal);

        for (var index = 1; index < items.Length; index++)
        {
            Assert.True(
                items[index - 1].GetProperty("bucketStartUtc").GetDateTime()
                < items[index].GetProperty("bucketStartUtc").GetDateTime());
        }
    }

    [Fact]
    public async Task GetTrend_WeeklyWindow_DoesNotReturnBucketsBeforeDeclaredRangeStart()
    {
        var caller = await RegisterAndLoginWithUserAsync("progress.trend.weekly.range@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/progress/trend?granularity=weekly&windowDays=7");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("weekly", payload.GetProperty("granularity").GetString());

        var rangeStartUtc = payload.GetProperty("rangeStartUtc").GetDateTime();
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.NotEmpty(items);
        Assert.All(
            items,
            item => Assert.True(item.GetProperty("bucketStartUtc").GetDateTime() >= rangeStartUtc));
    }

    [Fact]
    public async Task GetTrend_WithInvalidQuery_ReturnsValidationProblemDetails()
    {
        var caller = await RegisterAndLoginWithUserAsync("progress.trend.validation@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/progress/trend?granularity=hourly&windowDays=365");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("Validation failed", payload.GetProperty("title").GetString());
        Assert.Equal(400, payload.GetProperty("status").GetInt32());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        Assert.True(payload.GetProperty("errors").TryGetProperty("granularity", out _));
        Assert.True(payload.GetProperty("errors").TryGetProperty("windowDays", out _));
    }

    [Fact]
    public async Task GetXpSummary_WithoutAuthentication_ReturnsUnauthorizedProblemDetails()
    {
        var response = await _client.GetAsync("/api/v1/progress/xp-summary");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/authentication-failed", payload.GetProperty("type").GetString());
        Assert.Equal("Authentication Failed", payload.GetProperty("title").GetString());
        Assert.Equal(401, payload.GetProperty("status").GetInt32());
        Assert.Equal("auth.session.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    private async Task<(Guid UserId, LoginResponse Tokens)> RegisterAndLoginWithUserAsync(string email)
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass123!"));
        var registerPayload = (await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>())!;

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "StrongPass123!"));
        var loginPayload = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;

        return (registerPayload.UserId, loginPayload);
    }

    private async Task<TaskItem> SeedTaskAsync(Guid userId, string title)
    {
        var now = DateTime.UtcNow.AddMinutes(-30);
        return await _factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Description = $"{title} description",
            DueAtUtc = null,
            Priority = "medium",
            Category = "work",
            IsCompleted = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }

    private async Task SeedCompletionAndXpAsync(Guid userId, Guid taskId, DateTime occurredAtUtc, int xpGranted)
    {
        var completionEventId = Guid.NewGuid();

        await _factory.AddTaskCompletionEventAsync(new TaskCompletionEvent
        {
            Id = completionEventId,
            TaskId = taskId,
            OwnerId = userId,
            EventName = "TaskCompleted",
            ResultingIsCompleted = true,
            IdempotencyKey = Guid.NewGuid().ToString(),
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = occurredAtUtc
        });

        await _factory.AddXpLedgerEntryAsync(new XpLedgerEntry
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            TaskId = taskId,
            TaskCompletionEventId = completionEventId,
            EventName = "TaskCompleted",
            IdempotencyKey = Guid.NewGuid().ToString(),
            XpGranted = xpGranted,
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = occurredAtUtc
        });
    }
}
