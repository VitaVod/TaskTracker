using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Tests.Integration;

public class TasksControllerTests : IClassFixture<AuthTestFactory>
{
    private readonly HttpClient _client;
    private readonly AuthTestFactory _factory;

    public TasksControllerTests(AuthTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Create_WithValidPayload_ReturnsCreatedAndPersistsOwnedTask()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.create.valid@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tasks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        request.Content = JsonContent.Create(new
        {
            title = "Plan sprint backlog",
            description = "Draft story priorities for next sprint",
            dueAtUtc = "2026-04-27T18:00:00Z",
            priority = "Medium",
            category = "Work"
        });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("Plan sprint backlog", payload.GetProperty("title").GetString());
        Assert.Equal("medium", payload.GetProperty("priority").GetString());
        Assert.Equal("work", payload.GetProperty("category").GetString());
        Assert.False(payload.GetProperty("isCompleted").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("createdAtUtc").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("updatedAtUtc").GetString()));

        var taskId = payload.GetProperty("id").GetGuid();
        var persistedTask = await _factory.FindTaskByIdAsync(taskId);

        Assert.NotNull(persistedTask);
        Assert.Equal(caller.UserId, persistedTask!.UserId);
        Assert.Equal("medium", persistedTask.Priority);
        Assert.Equal("work", persistedTask.Category);
        Assert.False(persistedTask.IsCompleted);
    }

    [Fact]
    public async Task Create_WithInvalidPayload_ReturnsValidationProblemDetailsAndDoesNotPersist()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.create.invalid@example.com");
        var countBefore = await _factory.CountTasksForUserAsync(caller.UserId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tasks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        request.Content = JsonContent.Create(new
        {
            title = "",
            description = new string('x', 2001),
            priority = "urgent",
            category = ""
        });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("Validation failed", payload.GetProperty("title").GetString());
        Assert.Equal(400, payload.GetProperty("status").GetInt32());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        Assert.True(payload.GetProperty("errors").TryGetProperty("title", out _));
        Assert.True(payload.GetProperty("errors").TryGetProperty("description", out _));
        Assert.True(payload.GetProperty("errors").TryGetProperty("priority", out _));
        Assert.True(payload.GetProperty("errors").TryGetProperty("category", out _));

        var countAfter = await _factory.CountTasksForUserAsync(caller.UserId);
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task Create_WithMalformedDateType_ReturnsValidationProblemDetailsWithStableCode()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.create.malformed@example.com");
        var countBefore = await _factory.CountTasksForUserAsync(caller.UserId);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tasks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        request.Content = JsonContent.Create(new
        {
            title = "Malformed due date",
            description = "invalid dueAtUtc type",
            dueAtUtc = "not-a-date",
            priority = "medium",
            category = "work"
        });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("Validation failed", payload.GetProperty("title").GetString());
        Assert.Equal(400, payload.GetProperty("status").GetInt32());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        Assert.True(payload.GetProperty("errors").TryGetProperty("request", out _)
            || payload.GetProperty("errors").TryGetProperty("dueAtUtc", out _));

        var countAfter = await _factory.CountTasksForUserAsync(caller.UserId);
        Assert.Equal(countBefore, countAfter);
    }

    [Fact]
    public async Task Create_WithoutAuthentication_ReturnsUnauthorizedProblemDetails()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/tasks", new
        {
            title = "Unauthenticated attempt",
            priority = "medium",
            category = "planning"
        });

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/authentication-failed", payload.GetProperty("type").GetString());
        Assert.Equal("Authentication Failed", payload.GetProperty("title").GetString());
        Assert.Equal(401, payload.GetProperty("status").GetInt32());
        Assert.Equal("auth.session.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Create_WithPayloadUserId_DoesNotAllowOwnershipImpersonation()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.owner.caller@example.com");
        var targetUser = await RegisterAndLoginWithUserAsync("tasks.owner.target@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/tasks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var body = $$"""
        {
          "userId": "{{targetUser.UserId}}",
          "title": "Ownership test",
          "description": "Attempt to spoof owner",
          "priority": "medium",
                    "category": "work"
        }
        """;

        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var taskId = payload.GetProperty("id").GetGuid();
        var persistedTask = await _factory.FindTaskByIdAsync(taskId);

        Assert.NotNull(persistedTask);
        Assert.Equal(caller.UserId, persistedTask!.UserId);
        Assert.NotEqual(targetUser.UserId, persistedTask.UserId);
    }

    [Fact]
    public async Task List_WithoutStateFilter_ReturnsOwnedTasksWithSummaryAndDeterministicOrdering()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.list.default@example.com");
        var baseTime = new DateTime(2026, 4, 25, 12, 0, 0, DateTimeKind.Utc);

        var activeNewest = await SeedTaskAsync(caller.UserId, "Active newest", false, baseTime.AddMinutes(3));
        var activeOldest = await SeedTaskAsync(caller.UserId, "Active oldest", false, baseTime.AddMinutes(1));
        var completedTask = await SeedTaskAsync(caller.UserId, "Completed", true, baseTime.AddMinutes(2));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tasks");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, payload.GetProperty("summary").GetProperty("activeCount").GetInt32());
        Assert.Equal(1, payload.GetProperty("summary").GetProperty("completedCount").GetInt32());

        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(3, items.Length);

        Assert.Equal(activeNewest.Id, items[0].GetProperty("id").GetGuid());
        Assert.False(items[0].GetProperty("isCompleted").GetBoolean());

        Assert.Equal(activeOldest.Id, items[1].GetProperty("id").GetGuid());
        Assert.False(items[1].GetProperty("isCompleted").GetBoolean());

        Assert.Equal(completedTask.Id, items[2].GetProperty("id").GetGuid());
        Assert.True(items[2].GetProperty("isCompleted").GetBoolean());
    }

    [Fact]
    public async Task List_WithCompletedStateFilter_ReturnsOnlyCompletedOwnedTasks()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.list.completed@example.com");
        var baseTime = new DateTime(2026, 4, 25, 12, 30, 0, DateTimeKind.Utc);

        await SeedTaskAsync(caller.UserId, "Active task", false, baseTime.AddMinutes(1));
        var completedTask = await SeedTaskAsync(caller.UserId, "Completed task", true, baseTime.AddMinutes(2));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tasks?state=completed");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal(completedTask.Id, items[0].GetProperty("id").GetGuid());
        Assert.True(items[0].GetProperty("isCompleted").GetBoolean());

        Assert.Equal(1, payload.GetProperty("summary").GetProperty("activeCount").GetInt32());
        Assert.Equal(1, payload.GetProperty("summary").GetProperty("completedCount").GetInt32());
    }

    [Fact]
    public async Task List_DoesNotReturnTasksOwnedByAnotherUser()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.list.owner-a@example.com");
        var otherUser = await RegisterAndLoginWithUserAsync("tasks.list.owner-b@example.com");

        var ownTask = await SeedTaskAsync(caller.UserId, "Caller task", false, DateTime.UtcNow.AddMinutes(1));
        await SeedTaskAsync(otherUser.UserId, "Other user task", true, DateTime.UtcNow.AddMinutes(2));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tasks?state=all");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal(ownTask.Id, items[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task List_WithInvalidStateFilter_ReturnsValidationProblemDetails()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.list.invalid-state@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tasks?state=archived");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("Validation failed", payload.GetProperty("title").GetString());
        Assert.Equal(400, payload.GetProperty("status").GetInt32());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));

        var errors = payload.GetProperty("errors");
        Assert.True(errors.TryGetProperty("state", out var stateErrors));
        Assert.Contains("must be one of", stateErrors[0].GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Update_WithOwnedTask_UpdatesAllowedFieldsAndReturnsUpdatedPayload()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.update.owned@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Original title", false, DateTime.UtcNow.AddMinutes(-2));

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/tasks/{task.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        request.Content = JsonContent.Create(new
        {
            title = "Updated title",
            description = "Updated description",
            dueAtUtc = "2026-04-28T17:00:00Z",
            priority = "High",
            category = "Work"
        });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(task.Id, payload.GetProperty("id").GetGuid());
        Assert.Equal("Updated title", payload.GetProperty("title").GetString());
        Assert.Equal("Updated description", payload.GetProperty("description").GetString());
        Assert.Equal("high", payload.GetProperty("priority").GetString());
        Assert.Equal("work", payload.GetProperty("category").GetString());

        var updatedAtUtc = payload.GetProperty("updatedAtUtc").GetDateTime();
        Assert.True(updatedAtUtc >= task.UpdatedAtUtc);

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.NotNull(persistedTask);
        Assert.Equal("Updated title", persistedTask!.Title);
        Assert.Equal("Updated description", persistedTask.Description);
        Assert.Equal("high", persistedTask.Priority);
        Assert.Equal("work", persistedTask.Category);
        Assert.Equal(task.UserId, persistedTask.UserId);
        Assert.False(persistedTask.IsCompleted);
    }

    [Fact]
    public async Task Update_WithoutAuthentication_ReturnsUnauthorizedProblemDetails()
    {
        var taskId = Guid.NewGuid();

        var response = await _client.PutAsJsonAsync($"/api/v1/tasks/{taskId}", new
        {
            title = "Unauthorized update",
            description = "no auth",
            dueAtUtc = "2026-04-28T17:00:00Z",
            priority = "medium",
            category = "work"
        });

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/authentication-failed", payload.GetProperty("type").GetString());
        Assert.Equal("Authentication Failed", payload.GetProperty("title").GetString());
        Assert.Equal(401, payload.GetProperty("status").GetInt32());
        Assert.Equal("auth.session.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Update_WithInvalidPayload_ReturnsValidationProblemDetailsAndDoesNotMutateTask()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.update.invalid@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Keep title", false, DateTime.UtcNow.AddMinutes(-2));

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/tasks/{task.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        request.Content = new StringContent(
            """
            {
              "title": "",
              "description": "invalid",
              "dueAtUtc": "2026-04-28T17:00:00",
              "priority": "urgent",
              "category": ""
            }
            """,
            Encoding.UTF8,
            "application/json");

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));

        var errors = payload.GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("priority", out _));
        Assert.True(errors.TryGetProperty("category", out _));
        Assert.True(errors.TryGetProperty("dueAtUtc", out _));

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.NotNull(persistedTask);
        Assert.Equal("Keep title", persistedTask!.Title);
        Assert.Equal("medium", persistedTask.Priority);
    }

    [Fact]
    public async Task Update_WithNonOwnedTask_ReturnsForbiddenAndDoesNotMutateTask()
    {
        var owner = await RegisterAndLoginWithUserAsync("tasks.update.owner@example.com");
        var attacker = await RegisterAndLoginWithUserAsync("tasks.update.attacker@example.com");
        var task = await SeedTaskAsync(owner.UserId, "Owner title", false, DateTime.UtcNow.AddMinutes(-3));

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/tasks/{task.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", attacker.Tokens.AccessToken);
        request.Content = JsonContent.Create(new
        {
            title = "Malicious update",
            description = "attempt",
            dueAtUtc = "2026-04-28T17:00:00Z",
            priority = "high",
            category = "work"
        });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.GetProperty("type").GetString());
        Assert.Equal("Forbidden", payload.GetProperty("title").GetString());
        Assert.Equal(403, payload.GetProperty("status").GetInt32());
        Assert.Equal("auth.forbidden", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.NotNull(persistedTask);
        Assert.Equal("Owner title", persistedTask!.Title);
        Assert.Equal(owner.UserId, persistedTask.UserId);
    }

    [Fact]
    public async Task ToggleCompletion_WithOwnedTask_CompletesTaskAndPersistsSingleProgressionEvent()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.completion.owned@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Toggle completion", false, DateTime.UtcNow.AddMinutes(-3));

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new
        {
            isCompleted = true
        });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.GetProperty("task").GetProperty("isCompleted").GetBoolean());
        Assert.True(payload.GetProperty("progression").GetProperty("eligibleForXp").GetBoolean());
        Assert.False(payload.GetProperty("progression").GetProperty("idempotentReplay").GetBoolean());
        Assert.Equal(10, payload.GetProperty("progression").GetProperty("xpGranted").GetInt32());
        Assert.Equal("restart", payload.GetProperty("progression").GetProperty("streak").GetProperty("outcome").GetString());
        Assert.Equal(1, payload.GetProperty("progression").GetProperty("streak").GetProperty("currentStreakDays").GetInt32());

        var updatedAtUtc = payload.GetProperty("task").GetProperty("updatedAtUtc").GetDateTime();
        Assert.True(updatedAtUtc >= task.UpdatedAtUtc);

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.NotNull(persistedTask);
        Assert.True(persistedTask!.IsCompleted);

        var completionEvents = await _factory.CountTaskCompletionEventsAsync(task.Id);
        var taskCompletedEvents = await _factory.CountTaskCompletedEventsAsync(task.Id);
        var xpLedgerEntries = await _factory.CountXpLedgerEntriesAsync(task.Id);
        Assert.Equal(1, completionEvents);
        Assert.Equal(1, taskCompletedEvents);
        Assert.Equal(1, xpLedgerEntries);
    }

    [Fact]
    public async Task ToggleCompletion_WithDuplicateIdempotencyKey_ReturnsStableStateWithoutDuplicateEvent()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.completion.idempotent@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Idempotency task", false, DateTime.UtcNow.AddMinutes(-3));
        var idempotencyKey = Guid.NewGuid().ToString();

        _factory.ClearCapturedLogs();

        using var firstRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        firstRequest.Headers.Add("Idempotency-Key", idempotencyKey);
        firstRequest.Content = JsonContent.Create(new { isCompleted = true });

        var firstResponse = await _client.SendAsync(firstRequest);
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstUpdatedAt = firstPayload.GetProperty("task").GetProperty("updatedAtUtc").GetDateTime();

        using var duplicateRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        duplicateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        duplicateRequest.Headers.Add("Idempotency-Key", idempotencyKey);
        duplicateRequest.Content = JsonContent.Create(new { isCompleted = false });

        var duplicateResponse = await _client.SendAsync(duplicateRequest);
        var duplicatePayload = await duplicateResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicateResponse.StatusCode);
        Assert.True(duplicatePayload.GetProperty("task").GetProperty("isCompleted").GetBoolean());
        Assert.Equal(firstUpdatedAt, duplicatePayload.GetProperty("task").GetProperty("updatedAtUtc").GetDateTime());
        Assert.True(duplicatePayload.GetProperty("progression").GetProperty("idempotentReplay").GetBoolean());
        Assert.Equal(
            firstPayload.GetProperty("progression").GetProperty("streak").GetProperty("outcome").GetString(),
            duplicatePayload.GetProperty("progression").GetProperty("streak").GetProperty("outcome").GetString());
        Assert.Equal(
            firstPayload.GetProperty("progression").GetProperty("completionEventId").GetGuid(),
            duplicatePayload.GetProperty("progression").GetProperty("completionEventId").GetGuid());
        Assert.Equal(
            firstPayload.GetProperty("progression").GetProperty("xpGranted").GetInt32(),
            duplicatePayload.GetProperty("progression").GetProperty("xpGranted").GetInt32());

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.NotNull(persistedTask);
        Assert.True(persistedTask!.IsCompleted);

        var completionEvents = await _factory.CountTaskCompletionEventsAsync(task.Id);
        var taskCompletedEvents = await _factory.CountTaskCompletedEventsAsync(task.Id);
        var xpLedgerEntries = await _factory.CountXpLedgerEntriesAsync(task.Id);
        Assert.Equal(1, completionEvents);
        Assert.Equal(1, taskCompletedEvents);
        Assert.Equal(1, xpLedgerEntries);

        var cacheInvalidationLogs = _factory.GetCapturedLogs()
            .Where(entry => entry.Category.Contains("SharedViewCacheCoordinator", StringComparison.Ordinal)
                && entry.Message.Contains("cache.invalidate", StringComparison.Ordinal)
                && entry.Message.Contains("scope=shared-views", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(cacheInvalidationLogs);
        Assert.Contains("TraceId", cacheInvalidationLogs[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ToggleCompletion_WithConcurrentDuplicateIdempotencyKey_ReturnsDeterministicResultWithoutDuplicateEvent()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.completion.idempotent.concurrent@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Concurrent idempotency task", false, DateTime.UtcNow.AddMinutes(-3));
        var idempotencyKey = Guid.NewGuid().ToString();

        using var requestA = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        requestA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        requestA.Headers.Add("Idempotency-Key", idempotencyKey);
        requestA.Content = JsonContent.Create(new { isCompleted = true });

        using var requestB = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        requestB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        requestB.Headers.Add("Idempotency-Key", idempotencyKey);
        requestB.Content = JsonContent.Create(new { isCompleted = true });

        var responses = await Task.WhenAll(_client.SendAsync(requestA), _client.SendAsync(requestB));
        var payloadA = await responses[0].Content.ReadFromJsonAsync<JsonElement>();
        var payloadB = await responses[1].Content.ReadFromJsonAsync<JsonElement>();

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.True(payloadA.GetProperty("task").GetProperty("isCompleted").GetBoolean());
        Assert.True(payloadB.GetProperty("task").GetProperty("isCompleted").GetBoolean());
        Assert.Equal(
            payloadA.GetProperty("task").GetProperty("updatedAtUtc").GetDateTime(),
            payloadB.GetProperty("task").GetProperty("updatedAtUtc").GetDateTime());
        Assert.Equal(
            payloadA.GetProperty("progression").GetProperty("xpGranted").GetInt32(),
            payloadB.GetProperty("progression").GetProperty("xpGranted").GetInt32());
        Assert.Equal(
            payloadA.GetProperty("progression").GetProperty("streak").GetProperty("outcome").GetString(),
            payloadB.GetProperty("progression").GetProperty("streak").GetProperty("outcome").GetString());

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.NotNull(persistedTask);
        Assert.True(persistedTask!.IsCompleted);

        var completionEvents = await _factory.CountTaskCompletionEventsAsync(task.Id);
        var taskCompletedEvents = await _factory.CountTaskCompletedEventsAsync(task.Id);
        var xpLedgerEntries = await _factory.CountXpLedgerEntriesAsync(task.Id);
        Assert.Equal(1, completionEvents);
        Assert.Equal(1, taskCompletedEvents);
        Assert.Equal(1, xpLedgerEntries);
    }

    [Fact]
    public async Task ToggleCompletion_WithConcurrentDifferentIdempotencyKeys_GrantsXpOnce()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.completion.idempotent.concurrent-different@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Concurrent different key task", false, DateTime.UtcNow.AddMinutes(-3));

        var firstKey = Guid.NewGuid().ToString();
        var secondKey = Guid.NewGuid().ToString();

        using var requestA = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        requestA.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        requestA.Headers.Add("Idempotency-Key", firstKey);
        requestA.Content = JsonContent.Create(new { isCompleted = true });

        using var requestB = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        requestB.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        requestB.Headers.Add("Idempotency-Key", secondKey);
        requestB.Content = JsonContent.Create(new { isCompleted = true });

        var responses = await Task.WhenAll(_client.SendAsync(requestA), _client.SendAsync(requestB));
        var payloadA = await responses[0].Content.ReadFromJsonAsync<JsonElement>();
        var payloadB = await responses[1].Content.ReadFromJsonAsync<JsonElement>();

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.True(payloadA.GetProperty("task").GetProperty("isCompleted").GetBoolean());
        Assert.True(payloadB.GetProperty("task").GetProperty("isCompleted").GetBoolean());

        var xpGranted = new[]
        {
            payloadA.GetProperty("progression").GetProperty("xpGranted").GetInt32(),
            payloadB.GetProperty("progression").GetProperty("xpGranted").GetInt32()
        };

        Assert.Contains(10, xpGranted);
        Assert.Contains(0, xpGranted);

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.NotNull(persistedTask);
        Assert.True(persistedTask!.IsCompleted);

        var completionEvents = await _factory.CountTaskCompletionEventsAsync(task.Id);
        var taskCompletedEvents = await _factory.CountTaskCompletedEventsAsync(task.Id);
        var xpLedgerEntries = await _factory.CountXpLedgerEntriesAsync(task.Id);
        Assert.Equal(2, completionEvents);
        Assert.Equal(1, taskCompletedEvents);
        Assert.Equal(1, xpLedgerEntries);
    }

    [Fact]
    public async Task ToggleCompletion_ReplayAfterTimezoneChange_ReturnsOriginalStreakProjection()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.completion.idempotent.timezone-replay@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Replay timezone stability", false, DateTime.UtcNow.AddMinutes(-3));
        var idempotencyKey = Guid.NewGuid().ToString();

        using var firstRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        firstRequest.Headers.Add("Idempotency-Key", idempotencyKey);
        firstRequest.Content = JsonContent.Create(new { isCompleted = true });

        var firstResponse = await _client.SendAsync(firstRequest);
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        await _factory.SetUserTimeZoneAsync(caller.UserId, "America/New_York");

        using var replayRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        replayRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        replayRequest.Headers.Add("Idempotency-Key", idempotencyKey);
        replayRequest.Content = JsonContent.Create(new { isCompleted = false });

        var replayResponse = await _client.SendAsync(replayRequest);
        var replayPayload = await replayResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.True(replayPayload.GetProperty("progression").GetProperty("idempotentReplay").GetBoolean());
        Assert.Equal(
            firstPayload.GetProperty("progression").GetProperty("streak").GetProperty("timeZoneId").GetString(),
            replayPayload.GetProperty("progression").GetProperty("streak").GetProperty("timeZoneId").GetString());
        Assert.Equal(
            firstPayload.GetProperty("progression").GetProperty("streak").GetProperty("evaluationWindowStartUtc").GetString(),
            replayPayload.GetProperty("progression").GetProperty("streak").GetProperty("evaluationWindowStartUtc").GetString());
        Assert.Equal(
            firstPayload.GetProperty("progression").GetProperty("streak").GetProperty("evaluationWindowEndUtc").GetString(),
            replayPayload.GetProperty("progression").GetProperty("streak").GetProperty("evaluationWindowEndUtc").GetString());
    }

    [Fact]
    public async Task ToggleCompletion_WithInvalidPayload_ReturnsValidationProblemDetails()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.completion.invalid@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Invalid completion request", false, DateTime.UtcNow.AddMinutes(-3));

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        request.Headers.Add("Idempotency-Key", "invalid-guid");
        request.Content = JsonContent.Create(new { isCompleted = (bool?)null });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));

        var errors = payload.GetProperty("errors");
        Assert.True(errors.TryGetProperty("isCompleted", out _));
        Assert.True(errors.TryGetProperty("idempotencyKey", out _));
    }

    [Fact]
    public async Task ToggleCompletion_SetToFalse_ReturnsResetStreakOutcome()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.completion.reset@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Reset streak outcome", false, DateTime.UtcNow.AddMinutes(-10));

        using var completeRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        completeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        completeRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        completeRequest.Content = JsonContent.Create(new { isCompleted = true });

        var completeResponse = await _client.SendAsync(completeRequest);
        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);

        using var resetRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        resetRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        resetRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        resetRequest.Content = JsonContent.Create(new { isCompleted = false });

        var resetResponse = await _client.SendAsync(resetRequest);
        var resetPayload = await resetResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);
        Assert.Equal("reset", resetPayload.GetProperty("progression").GetProperty("streak").GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task ToggleCompletion_WithInvalidStoredTimeZone_ReturnsValidationProblemDetails()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.completion.invalid-timezone@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Invalid timezone", false, DateTime.UtcNow.AddMinutes(-3));
        await _factory.SetUserTimeZoneAsync(caller.UserId, "Invalid/Zone");

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { isCompleted = true });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("tasks.streak.timezone.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        Assert.True(payload.GetProperty("errors").TryGetProperty("timeZoneId", out _));
    }

    [Fact]
    public async Task ToggleCompletion_WithoutAuthentication_ReturnsUnauthorizedProblemDetails()
    {
        var taskId = Guid.NewGuid();

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{taskId}/completion");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { isCompleted = true });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/authentication-failed", payload.GetProperty("type").GetString());
        Assert.Equal("Authentication Failed", payload.GetProperty("title").GetString());
        Assert.Equal(401, payload.GetProperty("status").GetInt32());
        Assert.Equal("auth.session.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task ToggleCompletion_WithNonOwnedTask_ReturnsForbiddenAndDoesNotMutateTask()
    {
        var owner = await RegisterAndLoginWithUserAsync("tasks.completion.owner@example.com");
        var attacker = await RegisterAndLoginWithUserAsync("tasks.completion.attacker@example.com");
        var task = await SeedTaskAsync(owner.UserId, "Owner completion", false, DateTime.UtcNow.AddMinutes(-3));

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tasks/{task.Id}/completion");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", attacker.Tokens.AccessToken);
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Content = JsonContent.Create(new { isCompleted = true });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.GetProperty("type").GetString());
        Assert.Equal("auth.forbidden", payload.GetProperty("code").GetString());

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.NotNull(persistedTask);
        Assert.False(persistedTask!.IsCompleted);

        var completionEvents = await _factory.CountTaskCompletionEventsAsync(task.Id);
        var xpLedgerEntries = await _factory.CountXpLedgerEntriesAsync(task.Id);
        Assert.Equal(0, completionEvents);
        Assert.Equal(0, xpLedgerEntries);
    }

    [Fact]
    public async Task Delete_WithOwnedTask_ReturnsNoContentAndRemovesTaskFromOwnedLists()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.delete.owned@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Delete owned task", false, DateTime.UtcNow.AddMinutes(-2));

        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/tasks/{task.Id}");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.Null(persistedTask);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/tasks?state=all");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var listResponse = await _client.SendAsync(listRequest);
        var listPayload = await listResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.Empty(listPayload.GetProperty("items").EnumerateArray());
        Assert.Equal(0, listPayload.GetProperty("summary").GetProperty("activeCount").GetInt32());
        Assert.Equal(0, listPayload.GetProperty("summary").GetProperty("completedCount").GetInt32());
    }

    [Fact]
    public async Task Delete_WhenRepeated_ReturnsNoContentDeterministically()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.delete.idempotent@example.com");
        var task = await SeedTaskAsync(caller.UserId, "Delete idempotent task", false, DateTime.UtcNow.AddMinutes(-2));

        using var firstRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/tasks/{task.Id}");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/tasks/{task.Id}");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var firstResponse = await _client.SendAsync(firstRequest);
        var secondResponse = await _client.SendAsync(secondRequest);

        Assert.Equal(HttpStatusCode.NoContent, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, secondResponse.StatusCode);

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.Null(persistedTask);
    }

    [Fact]
    public async Task Delete_WithMalformedTaskId_ReturnsValidationProblemDetails()
    {
        var caller = await RegisterAndLoginWithUserAsync("tasks.delete.invalid-id@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/tasks/not-a-guid");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", caller.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("Validation failed", payload.GetProperty("title").GetString());
        Assert.Equal(400, payload.GetProperty("status").GetInt32());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        Assert.True(payload.GetProperty("errors").TryGetProperty("taskId", out _));
    }

    [Fact]
    public async Task Delete_WithoutAuthentication_ReturnsUnauthorizedProblemDetails()
    {
        var response = await _client.DeleteAsync($"/api/v1/tasks/{Guid.NewGuid()}");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/authentication-failed", payload.GetProperty("type").GetString());
        Assert.Equal("Authentication Failed", payload.GetProperty("title").GetString());
        Assert.Equal(401, payload.GetProperty("status").GetInt32());
        Assert.Equal("auth.session.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Delete_WithNonOwnedTask_ReturnsForbiddenAndDoesNotDeleteOwnerTask()
    {
        var owner = await RegisterAndLoginWithUserAsync("tasks.delete.owner@example.com");
        var attacker = await RegisterAndLoginWithUserAsync("tasks.delete.attacker@example.com");
        var task = await SeedTaskAsync(owner.UserId, "Owner delete protection", false, DateTime.UtcNow.AddMinutes(-2));

        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/tasks/{task.Id}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", attacker.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.GetProperty("type").GetString());
        Assert.Equal("Forbidden", payload.GetProperty("title").GetString());
        Assert.Equal(403, payload.GetProperty("status").GetInt32());
        Assert.Equal("auth.forbidden", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));

        var persistedTask = await _factory.FindTaskByIdAsync(task.Id);
        Assert.NotNull(persistedTask);
        Assert.Equal(owner.UserId, persistedTask!.UserId);
    }

    private async Task<(Guid UserId, LoginResponse Tokens)> RegisterAndLoginWithUserAsync(string email)
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass123!"));
        var registerPayload = (await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>())!;

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "StrongPass123!"));
        var loginPayload = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;

        return (registerPayload.UserId, loginPayload);
    }

    private async Task<TaskItem> SeedTaskAsync(Guid userId, string title, bool isCompleted, DateTime updatedAtUtc)
    {
        var now = updatedAtUtc.AddMinutes(-1);
        return await _factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Description = $"{title} description",
            DueAtUtc = null,
            Priority = "medium",
            Category = "work",
            IsCompleted = isCompleted,
            CreatedAtUtc = now,
            UpdatedAtUtc = updatedAtUtc
        });
    }
}