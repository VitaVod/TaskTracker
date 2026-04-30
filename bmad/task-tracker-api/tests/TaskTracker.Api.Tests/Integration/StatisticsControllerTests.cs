using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Tests.Integration;

public class StatisticsControllerTests
{
    [Fact]
    public async Task GetGlobal_WithAuthenticatedUser_ReturnsDeterministicTaskCounters()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "statistics.caller@example.com");
        var second = await RegisterAndLoginWithUserAsync(client, "statistics.second@example.com");

        await factory.AddTaskAsync(CreateTask(caller.UserId, false));
        await factory.AddTaskAsync(CreateTask(caller.UserId, true));
        await factory.AddTaskAsync(CreateTask(second.UserId, true));

        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/statistics/global");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/statistics/global");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var firstResponse = await client.SendAsync(firstRequest);
        var secondResponse = await client.SendAsync(secondRequest);

        var firstPayload = (await firstResponse.Content.ReadFromJsonAsync<JsonElement>())!;
        var secondPayload = (await secondResponse.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstPayload.ToString(), secondPayload.ToString());
        Assert.Equal(3, firstPayload.GetProperty("totalTasksCreated").GetInt64());
        Assert.Equal(2, firstPayload.GetProperty("totalTasksCompleted").GetInt64());
    }

    [Fact]
    public async Task GetGlobal_WithoutAuthentication_ReturnsUnauthorizedProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/statistics/global");
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/authentication-failed", payload.GetProperty("type").GetString());
        Assert.Equal("Authentication Failed", payload.GetProperty("title").GetString());
        Assert.Equal(401, payload.GetProperty("status").GetInt32());
        Assert.Equal("auth.session.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task GetGlobal_WritesStructuredLogWithTraceIdentifier()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "statistics.logging@example.com");
        await factory.AddTaskAsync(CreateTask(caller.UserId, true));

        factory.ClearCapturedLogs();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/statistics/global");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var logs = factory.GetCapturedLogs();
        Assert.Contains(logs, entry =>
            entry.Category.Contains("StatisticsController", StringComparison.Ordinal)
            && entry.Message.Contains("Global task statistics served", StringComparison.Ordinal)
            && entry.Message.Contains("TraceId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetGlobal_UsesCacheAndRefreshesAfterCompletionCommit()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "statistics.cache.caller@example.com");
        var task = await factory.AddTaskAsync(CreateTask(caller.UserId, false));

        factory.ClearCapturedLogs();

        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/statistics/global");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/statistics/global");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var firstResponse = await client.SendAsync(firstRequest);
        var secondResponse = await client.SendAsync(secondRequest);

        var firstPayload = (await firstResponse.Content.ReadFromJsonAsync<JsonElement>())!;
        var secondPayload = (await secondResponse.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstPayload.ToString(), secondPayload.ToString());

        using var toggleRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        toggleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        toggleRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        toggleRequest.Content = JsonContent.Create(new { isCompleted = true });

        var toggleResponse = await client.SendAsync(toggleRequest);
        Assert.Equal(HttpStatusCode.OK, toggleResponse.StatusCode);

        using var refreshedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/statistics/global");
        refreshedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        var refreshedResponse = await client.SendAsync(refreshedRequest);
        var refreshedPayload = (await refreshedResponse.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, refreshedResponse.StatusCode);
        Assert.Equal(firstPayload.GetProperty("totalTasksCreated").GetInt64(), refreshedPayload.GetProperty("totalTasksCreated").GetInt64());
        Assert.Equal(firstPayload.GetProperty("totalTasksCompleted").GetInt64() + 1, refreshedPayload.GetProperty("totalTasksCompleted").GetInt64());

        var cacheLogs = factory.GetCapturedLogs()
            .Where(entry => entry.Category.Contains("SharedViewCacheCoordinator", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(cacheLogs, entry => entry.Message.Contains("cache.miss", StringComparison.Ordinal) && entry.Message.Contains("statistics:global", StringComparison.Ordinal));
        Assert.Contains(cacheLogs, entry => entry.Message.Contains("cache.hit", StringComparison.Ordinal) && entry.Message.Contains("statistics:global", StringComparison.Ordinal));
        Assert.Contains(cacheLogs, entry => entry.Message.Contains("cache.refresh", StringComparison.Ordinal) && entry.Message.Contains("TraceId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cacheLogs, entry => entry.Message.Contains("cache.invalidate", StringComparison.Ordinal) && entry.Message.Contains("TraceId", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<(Guid UserId, LoginResponse Tokens)> RegisterAndLoginWithUserAsync(HttpClient client, string email)
    {
        var registerResponse = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass123!"));
        var registerPayload = (await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>())!;

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "StrongPass123!"));
        var loginPayload = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;

        return (registerPayload.UserId, loginPayload);
    }

    private static TaskItem CreateTask(Guid userId, bool isCompleted)
    {
        var now = DateTime.UtcNow;

        return new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = $"Task {Guid.NewGuid():N}",
            Description = "Statistics task fixture",
            DueAtUtc = null,
            Priority = "medium",
            Category = "work",
            IsCompleted = isCompleted,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
