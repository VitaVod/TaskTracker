using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Tests.Integration;

public class LeaderboardsControllerTests
{
    [Fact]
    public async Task Get_WithStreakType_ReturnsDeterministicOrderingWithTieBreaks()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.streak.caller@example.com");
        var first = await RegisterAndLoginWithUserAsync(client, "leaderboard.streak.first@example.com");
        var second = await RegisterAndLoginWithUserAsync(client, "leaderboard.streak.second@example.com");
        var third = await RegisterAndLoginWithUserAsync(client, "leaderboard.streak.third@example.com");

        await factory.SetLeaderboardParticipationModeAsync(caller.UserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetLeaderboardParticipationModeAsync(first.UserId, LeaderboardParticipationMode.Public);
        await factory.SetLeaderboardParticipationModeAsync(second.UserId, LeaderboardParticipationMode.Public);
        await factory.SetLeaderboardParticipationModeAsync(third.UserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetUserDisplayNameAsync(first.UserId, "First Focus");
        await factory.SetUserDisplayNameAsync(second.UserId, "Second Focus");

        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(caller.UserId, 1));
        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(first.UserId, 9));
        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(second.UserId, 9));
        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(third.UserId, 6));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/leaderboards?type=streak&page=1&pageSize=4");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("streak", payload.GetProperty("type").GetString());
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(4, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal(4, payload.GetProperty("totalCount").GetInt32());
        Assert.False(payload.GetProperty("hasNextPage").GetBoolean());

        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        var expectedOrder = new[] { first.UserId, second.UserId, third.UserId, caller.UserId }
            .OrderByDescending(userId => userId == first.UserId || userId == second.UserId ? 9 : userId == third.UserId ? 6 : 1)
            .ThenBy(userId => userId)
            .ToArray();

        for (var index = 0; index < expectedOrder.Length; index++)
        {
            Assert.Equal(index + 1, items[index].GetProperty("rank").GetInt32());
            var avatarMarker = items[index].GetProperty("avatarMarker").GetString();
            Assert.False(string.IsNullOrWhiteSpace(avatarMarker));
            Assert.StartsWith("avatar-", avatarMarker, StringComparison.Ordinal);

            var userIdFragment = expectedOrder[index].ToString("N")[..6];
            Assert.DoesNotContain(userIdFragment, avatarMarker!, StringComparison.OrdinalIgnoreCase);

            Assert.False(items[index].TryGetProperty("email", out _));
            Assert.False(items[index].TryGetProperty("displayName", out _));
            Assert.False(items[index].TryGetProperty("timeZoneId", out _));
            Assert.False(items[index].TryGetProperty("locale", out _));
            Assert.False(items[index].TryGetProperty("passwordHash", out _));
            Assert.False(items[index].TryGetProperty("passwordSalt", out _));
            Assert.False(items[index].TryGetProperty("userId", out _));
        }

        var topTied = new[] { first.UserId, second.UserId }.OrderBy(userId => userId).ToArray();
        var firstExpectedPublicName = topTied[0] == first.UserId ? "First Focus" : "Second Focus";
        var secondExpectedPublicName = topTied[1] == first.UserId ? "First Focus" : "Second Focus";

        Assert.Equal("public", items[0].GetProperty("identityMode").GetString());
        Assert.Equal(firstExpectedPublicName, items[0].GetProperty("publicIdentity").GetString());
        Assert.Equal(ToPublicProfileHandle(topTied[0]), items[0].GetProperty("publicProfileHandle").GetString());
        Assert.Equal("public", items[1].GetProperty("identityMode").GetString());
        Assert.Equal(secondExpectedPublicName, items[1].GetProperty("publicIdentity").GetString());
        Assert.Equal(ToPublicProfileHandle(topTied[1]), items[1].GetProperty("publicProfileHandle").GetString());
        Assert.Equal("anonymous", items[2].GetProperty("identityMode").GetString());
        Assert.Equal(ToAnonymousIdentity(third.UserId), items[2].GetProperty("publicIdentity").GetString());
        Assert.Equal(JsonValueKind.Null, items[2].GetProperty("publicProfileHandle").ValueKind);
        Assert.Equal("anonymous", items[3].GetProperty("identityMode").GetString());
        Assert.Equal(ToAnonymousIdentity(caller.UserId), items[3].GetProperty("publicIdentity").GetString());
        Assert.Equal(JsonValueKind.Null, items[3].GetProperty("publicProfileHandle").ValueKind);

        Assert.Equal(9, items[0].GetProperty("metricValue").GetInt32());
        Assert.Equal(9, items[1].GetProperty("metricValue").GetInt32());
        Assert.Equal(6, items[2].GetProperty("metricValue").GetInt32());
        Assert.Equal(1, items[3].GetProperty("metricValue").GetInt32());
    }

    [Fact]
    public async Task Get_WithCompletedTasksType_ReturnsDeterministicOrderingAcrossRepeatedCalls()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.completed.caller@example.com");
        var first = await RegisterAndLoginWithUserAsync(client, "leaderboard.completed.first@example.com");
        var second = await RegisterAndLoginWithUserAsync(client, "leaderboard.completed.second@example.com");
        var third = await RegisterAndLoginWithUserAsync(client, "leaderboard.completed.third@example.com");

        await factory.SetLeaderboardParticipationModeAsync(caller.UserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetLeaderboardParticipationModeAsync(first.UserId, LeaderboardParticipationMode.Public);
        await factory.SetLeaderboardParticipationModeAsync(second.UserId, LeaderboardParticipationMode.Public);
        await factory.SetLeaderboardParticipationModeAsync(third.UserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetUserDisplayNameAsync(first.UserId, "Task Master One");
        await factory.SetUserDisplayNameAsync(second.UserId, "Task Master Two");

        await SeedCompletedEventsAsync(factory, caller.UserId, 1);
        await SeedCompletedEventsAsync(factory, first.UserId, 3);
        await SeedCompletedEventsAsync(factory, second.UserId, 3);
        await SeedCompletedEventsAsync(factory, third.UserId, 2);

        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/leaderboards?type=completedTasks&page=1&pageSize=4");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/leaderboards?type=completedTasks&page=1&pageSize=4");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var firstResponse = await client.SendAsync(firstRequest);
        var secondResponse = await client.SendAsync(secondRequest);
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal(firstPayload.ToString(), secondPayload.ToString());

        var items = firstPayload.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(3, items[0].GetProperty("metricValue").GetInt32());
        Assert.Equal(3, items[1].GetProperty("metricValue").GetInt32());
        Assert.Equal(2, items[2].GetProperty("metricValue").GetInt32());
        Assert.Equal(1, items[3].GetProperty("metricValue").GetInt32());

        var tiedTop = new[] { first.UserId, second.UserId }.OrderBy(userId => userId).ToArray();
        var firstExpectedName = tiedTop[0] == first.UserId ? "Task Master One" : "Task Master Two";
        var secondExpectedName = tiedTop[1] == first.UserId ? "Task Master One" : "Task Master Two";
        Assert.Equal(firstExpectedName, items[0].GetProperty("publicIdentity").GetString());
        Assert.Equal("public", items[0].GetProperty("identityMode").GetString());
        Assert.Equal(secondExpectedName, items[1].GetProperty("publicIdentity").GetString());
        Assert.Equal("public", items[1].GetProperty("identityMode").GetString());
    }

    [Fact]
    public async Task Get_WithPagination_ReturnsCorrectMetadataForFirstMiddleAndLastPages()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.pagination.caller@example.com");
        var users = new[]
        {
            await RegisterAndLoginWithUserAsync(client, "leaderboard.pagination.1@example.com"),
            await RegisterAndLoginWithUserAsync(client, "leaderboard.pagination.2@example.com"),
            await RegisterAndLoginWithUserAsync(client, "leaderboard.pagination.3@example.com"),
            await RegisterAndLoginWithUserAsync(client, "leaderboard.pagination.4@example.com")
        };

        await factory.SetLeaderboardParticipationModeAsync(caller.UserId, LeaderboardParticipationMode.Anonymous);
        foreach (var user in users)
        {
            await factory.SetLeaderboardParticipationModeAsync(user.UserId, LeaderboardParticipationMode.Anonymous);
        }

        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(caller.UserId, 50));
        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(users[0].UserId, 40));
        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(users[1].UserId, 30));
        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(users[2].UserId, 20));
        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(users[3].UserId, 10));

        var page1 = await GetLeaderboardPayloadAsync(client, caller.Tokens.AccessToken, "streak", 1, 2);
        var page2 = await GetLeaderboardPayloadAsync(client, caller.Tokens.AccessToken, "streak", 2, 2);
        var page3 = await GetLeaderboardPayloadAsync(client, caller.Tokens.AccessToken, "streak", 3, 2);

        Assert.Equal(1, page1.GetProperty("page").GetInt32());
        Assert.Equal(2, page1.GetProperty("pageSize").GetInt32());
        Assert.Equal(5, page1.GetProperty("totalCount").GetInt32());
        Assert.True(page1.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(2, page1.GetProperty("items").GetArrayLength());

        Assert.Equal(2, page2.GetProperty("page").GetInt32());
        Assert.True(page2.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(2, page2.GetProperty("items").GetArrayLength());

        Assert.Equal(3, page3.GetProperty("page").GetInt32());
        Assert.False(page3.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal(1, page3.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Get_WhenParticipantIsHidden_ExcludesUserFromLeaderboardItemsAndCounts()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.hidden.caller@example.com");
        var visible = await RegisterAndLoginWithUserAsync(client, "leaderboard.hidden.visible@example.com");
        var hidden = await RegisterAndLoginWithUserAsync(client, "leaderboard.hidden.hidden@example.com");

        await factory.SetLeaderboardParticipationModeAsync(caller.UserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetLeaderboardParticipationModeAsync(visible.UserId, LeaderboardParticipationMode.Public);
        await factory.SetLeaderboardParticipationModeAsync(hidden.UserId, LeaderboardParticipationMode.Hidden);
        await factory.SetUserDisplayNameAsync(visible.UserId, "Visible Peer");

        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(caller.UserId, 5));
        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(visible.UserId, 7));
        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(hidden.UserId, 99));

        var payload = await GetLeaderboardPayloadAsync(client, caller.Tokens.AccessToken, "streak", 1, 10);

        Assert.Equal(2, payload.GetProperty("totalCount").GetInt32());
        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal("Visible Peer", items[0].GetProperty("publicIdentity").GetString());
        Assert.DoesNotContain(items, item => string.Equals(item.GetProperty("publicIdentity").GetString(), ToAnonymousIdentity(hidden.UserId), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Get_WhenPublicModeHasNoDisplayName_UsesDeterministicAnonymousFallback()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.fallback.caller@example.com");
        var publicWithoutAlias = await RegisterAndLoginWithUserAsync(client, "leaderboard.fallback.public@example.com");

        await factory.SetLeaderboardParticipationModeAsync(caller.UserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetLeaderboardParticipationModeAsync(publicWithoutAlias.UserId, LeaderboardParticipationMode.Public);

        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(caller.UserId, 1));
        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(publicWithoutAlias.UserId, 2));

        var payload = await GetLeaderboardPayloadAsync(client, caller.Tokens.AccessToken, "streak", 1, 10);
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(ToAnonymousIdentity(publicWithoutAlias.UserId), items[0].GetProperty("publicIdentity").GetString());
        Assert.Equal("anonymous", items[0].GetProperty("identityMode").GetString());
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("publicProfileHandle").ValueKind);
    }

    [Fact]
    public async Task GetProfile_WhenParticipantIsPublic_ReturnsApprovedStatistics()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.profile.caller@example.com");
        var target = await RegisterAndLoginWithUserAsync(client, "leaderboard.profile.target@example.com");

        await factory.SetLeaderboardParticipationModeAsync(caller.UserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetLeaderboardParticipationModeAsync(target.UserId, LeaderboardParticipationMode.Public);
        await factory.SetUserDisplayNameAsync(target.UserId, "Public Pace");

        await factory.UpsertStreakSnapshotAsync(CreateSnapshot(target.UserId, 8));
        await SeedCompletedEventsAsync(factory, target.UserId, 3);

        var payload = await GetPublicProfilePayloadAsync(
            client,
            caller.Tokens.AccessToken,
            ToPublicProfileHandle(target.UserId));

        Assert.Equal("public", payload.GetProperty("visibility").GetString());
        Assert.Equal("Public Pace", payload.GetProperty("publicIdentity").GetString());
        Assert.StartsWith("avatar-", payload.GetProperty("avatarMarker").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("message").ValueKind);

        var statistics = payload.GetProperty("statistics");
        Assert.Equal(8, statistics.GetProperty("currentStreakDays").GetInt32());
        Assert.Equal(8, statistics.GetProperty("longestStreakDays").GetInt32());
        Assert.Equal(3, statistics.GetProperty("completedTaskCount").GetInt32());

        Assert.False(payload.TryGetProperty("email", out _));
        Assert.False(payload.TryGetProperty("userId", out _));
        Assert.False(payload.TryGetProperty("timeZoneId", out _));
        Assert.False(payload.TryGetProperty("locale", out _));
    }

    [Fact]
    public async Task GetProfile_WhenParticipantIsAnonymousOrHandleInvalid_ReturnsDeterministicAnonymousResponse()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.profile.guard.caller@example.com");
        var anonymousTarget = await RegisterAndLoginWithUserAsync(client, "leaderboard.profile.guard.target@example.com");

        await factory.SetLeaderboardParticipationModeAsync(caller.UserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetLeaderboardParticipationModeAsync(anonymousTarget.UserId, LeaderboardParticipationMode.Anonymous);

        var anonymousPayload = await GetPublicProfilePayloadAsync(
            client,
            caller.Tokens.AccessToken,
            ToPublicProfileHandle(anonymousTarget.UserId));

        var invalidPayload = await GetPublicProfilePayloadAsync(
            client,
            caller.Tokens.AccessToken,
            $"p-{Guid.NewGuid():N}");

        Assert.Equal("anonymous", anonymousPayload.GetProperty("visibility").GetString());
        Assert.Equal("anonymous", invalidPayload.GetProperty("visibility").GetString());
        Assert.Equal(JsonValueKind.Null, anonymousPayload.GetProperty("statistics").ValueKind);
        Assert.Equal(JsonValueKind.Null, invalidPayload.GetProperty("statistics").ValueKind);
        Assert.Equal(
            anonymousPayload.GetProperty("message").GetString(),
            invalidPayload.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Get_WithDefaultPagination_UsesConfiguredDefaults()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.defaults.caller@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/leaderboards?type=streak");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(20, payload.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task Get_WithInvalidQuery_ReturnsValidationProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.validation.caller@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/leaderboards?type=unknown&page=0&pageSize=500");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("Validation failed", payload.GetProperty("title").GetString());
        Assert.Equal(400, payload.GetProperty("status").GetInt32());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.True(payload.GetProperty("errors").TryGetProperty("type", out _));
        Assert.True(payload.GetProperty("errors").TryGetProperty("page", out _));
        Assert.True(payload.GetProperty("errors").TryGetProperty("pageSize", out _));
    }

    [Fact]
    public async Task Get_WithOverflowingPage_ReturnsValidationProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.overflow.caller@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/leaderboards?type=streak&page=30000000&pageSize=100");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.True(payload.GetProperty("errors").TryGetProperty("page", out _));
    }

    [Fact]
    public async Task Get_WithoutAuthentication_ReturnsUnauthorizedProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/leaderboards?type=streak");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/authentication-failed", payload.GetProperty("type").GetString());
        Assert.Equal("Authentication Failed", payload.GetProperty("title").GetString());
        Assert.Equal(401, payload.GetProperty("status").GetInt32());
        Assert.Equal("auth.session.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Get_WithCompletedTasksType_UsesCacheAndRefreshesAfterCompletionInvalidation()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var caller = await RegisterAndLoginWithUserAsync(client, "leaderboard.cache.caller@example.com");
        var first = await RegisterAndLoginWithUserAsync(client, "leaderboard.cache.first@example.com");
        var second = await RegisterAndLoginWithUserAsync(client, "leaderboard.cache.second@example.com");
        var third = await RegisterAndLoginWithUserAsync(client, "leaderboard.cache.third@example.com");

        await factory.SetLeaderboardParticipationModeAsync(caller.UserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetLeaderboardParticipationModeAsync(first.UserId, LeaderboardParticipationMode.Public);
        await factory.SetLeaderboardParticipationModeAsync(second.UserId, LeaderboardParticipationMode.Public);
        await factory.SetLeaderboardParticipationModeAsync(third.UserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetUserDisplayNameAsync(first.UserId, "Cache Master One");
        await factory.SetUserDisplayNameAsync(second.UserId, "Cache Master Two");

        await SeedCompletedEventsAsync(factory, caller.UserId, 1);
        await SeedCompletedEventsAsync(factory, first.UserId, 3);
        await SeedCompletedEventsAsync(factory, second.UserId, 3);
        await SeedCompletedEventsAsync(factory, third.UserId, 2);

        factory.ClearCapturedLogs();

        var firstPayload = await GetLeaderboardPayloadAsync(client, caller.Tokens.AccessToken, "completedTasks", 1, 10, "first");
        var secondPayload = await GetLeaderboardPayloadAsync(client, caller.Tokens.AccessToken, "completedTasks", 1, 10, "second");

        var task = await factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = caller.UserId,
            Title = "Cache invalidation completion",
            Description = "Task completion should invalidate shared views",
            DueAtUtc = null,
            Priority = "medium",
            Category = "work",
            IsCompleted = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });

        using var toggleRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        toggleRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        toggleRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        toggleRequest.Content = JsonContent.Create(new { isCompleted = true });

        var toggleResponse = await client.SendAsync(toggleRequest);
        Assert.Equal(HttpStatusCode.OK, toggleResponse.StatusCode);

        var refreshedPayload = await GetLeaderboardPayloadAsync(client, caller.Tokens.AccessToken, "completedTasks", 1, 10, "refreshed");

        var firstItems = firstPayload.GetProperty("items").EnumerateArray().ToArray();
        var refreshedItems = refreshedPayload.GetProperty("items").EnumerateArray().ToArray();

        var callerIdentity = ToAnonymousIdentity(caller.UserId);
        var firstCallerMetric = firstItems.Single(item => item.GetProperty("publicIdentity").GetString() == callerIdentity)
            .GetProperty("metricValue").GetInt32();
        var refreshedCallerMetric = refreshedItems.Single(item => item.GetProperty("publicIdentity").GetString() == callerIdentity)
            .GetProperty("metricValue").GetInt32();

        Assert.Equal(firstCallerMetric + 1, refreshedCallerMetric);
        Assert.Equal(firstPayload.ToString(), secondPayload.ToString());

        var cacheLogs = factory.GetCapturedLogs()
            .Where(entry => entry.Category.Contains("SharedViewCacheCoordinator", StringComparison.Ordinal))
            .ToArray();

        Assert.Contains(cacheLogs, entry => entry.Message.Contains("cache.miss", StringComparison.Ordinal) && entry.Message.Contains("TraceId", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cacheLogs, entry => entry.Message.Contains("cache.hit", StringComparison.Ordinal) && entry.Message.Contains("TraceId", StringComparison.OrdinalIgnoreCase));
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

    private static UserStreakSnapshot CreateSnapshot(Guid ownerId, int currentStreakDays)
    {
        var now = DateTime.UtcNow;

        return new UserStreakSnapshot
        {
            OwnerId = ownerId,
            Outcome = TaskStreakOutcome.Continue,
            CurrentStreakDays = currentStreakDays,
            LongestStreakDays = currentStreakDays,
            TimeZoneId = "UTC",
            EvaluationWindowStartUtc = now.AddDays(-1),
            EvaluationWindowEndUtc = now,
            LastEvaluatedEventId = Guid.NewGuid(),
            LastEvaluationTraceId = "trace-leaderboard-tests",
            LastEvaluatedAtUtc = now
        };
    }

    private static async Task SeedCompletedEventsAsync(AuthTestFactory factory, Guid userId, int completedCount)
    {
        var now = DateTime.UtcNow;
        for (var index = 0; index < completedCount; index++)
        {
            var createdAtUtc = now.AddMinutes(-(index + 1));
            await factory.AddTaskAsync(new TaskItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = $"Completed seeded task {index + 1}",
                Description = "Leaderboard completed-task seeding",
                DueAtUtc = null,
                Priority = "medium",
                Category = "work",
                IsCompleted = true,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc
            });
        }
    }

    private static async Task<JsonElement> GetLeaderboardPayloadAsync(
        HttpClient client,
        string accessToken,
        string type,
        int page,
        int pageSize,
        string? step = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/leaderboards?type={type}&page={page}&pageSize={pageSize}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            var prefix = string.IsNullOrWhiteSpace(step) ? string.Empty : $"[{step}] ";
            throw new Xunit.Sdk.XunitException($"{prefix}Expected 200 OK but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        }

        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    private static async Task<JsonElement> GetPublicProfilePayloadAsync(
        HttpClient client,
        string accessToken,
        string profileHandle)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/leaderboards/profiles/{profileHandle}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        if (response.StatusCode != HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException($"Expected 200 OK but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        }

        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }

    private static string ToAnonymousIdentity(Guid userId)
    {
        return $"anon-{userId:N}"[..13];
    }

    private static string ToPublicProfileHandle(Guid userId)
    {
        return $"p-{userId:N}";
    }
}