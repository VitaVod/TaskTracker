using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Features.Integrations.Contracts;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Tests.Integration;

public class IntegrationsControllerTests : IClassFixture<AuthTestFactory>
{
    private readonly AuthTestFactory _factory;
    private readonly HttpClient _client;

    public IntegrationsControllerTests(AuthTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateCredential_WithTaskCreateSyncScope_AllowsIntegrationEndpoint()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.valid@example.com");

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/credentials");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Tokens.AccessToken);
        createRequest.Content = JsonContent.Create(new CreateIntegrationCredentialRequest(
            "automation-suite",
            "Automation Suite",
            ["tasks:create-sync"],
            DateTime.UtcNow.AddDays(30)));

        var createResponse = await _client.SendAsync(createRequest);
        var createPayload = await createResponse.Content.ReadFromJsonAsync<IntegrationCredentialCreatedResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createPayload);
        Assert.Equal(owner.UserId, createPayload.OwnerUserId);
        Assert.False(string.IsNullOrWhiteSpace(createPayload.Secret));

        using var integrationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/tasks/create-sync");
        integrationRequest.Headers.Add("X-Integration-Key-Id", createPayload.KeyId);
        integrationRequest.Headers.Add("X-Integration-Secret", createPayload.Secret);
        integrationRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        integrationRequest.Headers.Add("X-Correlation-Id", "corr-int-71-allow");
        integrationRequest.Content = JsonContent.Create(new IntegrationTaskCreateSyncRequest(
            "ext-1",
            "Sync Task",
            "Created via integration",
            DateTime.UtcNow.AddDays(2),
            "medium",
            "work",
            false));

        var integrationResponse = await _client.SendAsync(integrationRequest);
        var integrationPayload = await integrationResponse.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, integrationResponse.StatusCode);
        Assert.NotNull(integrationPayload);
        Assert.Equal("created", integrationPayload.Operation);
        Assert.False(integrationPayload.IdempotentReplay);
        Assert.Equal("automation-suite", integrationPayload.IntegrationId);
        Assert.Equal(owner.UserId, integrationPayload.OwnerUserId);
        Assert.Equal("corr-int-71-allow", integrationPayload.CorrelationId);

        var persistedTask = await _factory.FindTaskByIdAsync(integrationPayload.TaskId);
        Assert.NotNull(persistedTask);
        Assert.Equal(owner.UserId, persistedTask.UserId);
        Assert.Equal("Sync Task", persistedTask.Title);
        Assert.Equal("medium", persistedTask.Priority);
        Assert.Equal("work", persistedTask.Category);
    }

    [Fact]
    public async Task IntegrationEndpoint_RepeatedExternalTaskId_UpdatesExistingTask()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.upsert@example.com");
        var createdCredential = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        using var firstRequest = BuildCreateSyncRequest(
            createdCredential.KeyId,
            createdCredential.Secret,
            Guid.NewGuid().ToString(),
            new IntegrationTaskCreateSyncRequest(
                "ext-upsert-1",
                "Initial integration title",
                "initial description",
                DateTime.UtcNow.AddDays(1),
                "low",
                "planning",
                false));

        var firstResponse = await _client.SendAsync(firstRequest);
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPayload);
        Assert.Equal("created", firstPayload.Operation);
        Assert.False(firstPayload.IdempotentReplay);

        using var secondRequest = BuildCreateSyncRequest(
            createdCredential.KeyId,
            createdCredential.Secret,
            Guid.NewGuid().ToString(),
            new IntegrationTaskCreateSyncRequest(
                "ext-upsert-1",
                "Updated integration title",
                "updated description",
                DateTime.UtcNow.AddDays(3),
                "high",
                "work",
                true));

        var secondResponse = await _client.SendAsync(secondRequest);
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondPayload);
        Assert.Equal("updated", secondPayload.Operation);
        Assert.False(secondPayload.IdempotentReplay);
        Assert.Equal(firstPayload.TaskId, secondPayload.TaskId);

        var taskCount = await _factory.CountTasksForUserAsync(owner.UserId);
        Assert.Equal(1, taskCount);

        var persistedTask = await _factory.FindTaskByIdAsync(secondPayload.TaskId);
        Assert.NotNull(persistedTask);
        Assert.Equal(owner.UserId, persistedTask.UserId);
        Assert.Equal("Updated integration title", persistedTask.Title);
        Assert.Equal("updated description", persistedTask.Description);
        Assert.Equal("high", persistedTask.Priority);
        Assert.Equal("work", persistedTask.Category);
        Assert.True(persistedTask.IsCompleted);
    }

    [Fact]
    public async Task IntegrationEndpoint_RepeatedIdempotencyKey_ReturnsDeterministicReplayWithoutDuplicateMutation()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.replay@example.com");
        var createdCredential = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);
        var idempotencyKey = Guid.NewGuid().ToString();

        var payload = new IntegrationTaskCreateSyncRequest(
            "ext-replay-1",
            "Replay title",
            "Replay description",
            DateTime.UtcNow.AddDays(1),
            "medium",
            "work",
            false);

        using var firstRequest = BuildCreateSyncRequest(createdCredential.KeyId, createdCredential.Secret, idempotencyKey, payload);
        var firstResponse = await _client.SendAsync(firstRequest);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstBody);
        Assert.Equal("created", firstBody.Operation);
        Assert.False(firstBody.IdempotentReplay);

        using var secondRequest = BuildCreateSyncRequest(createdCredential.KeyId, createdCredential.Secret, idempotencyKey, payload);
        var secondResponse = await _client.SendAsync(secondRequest);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondBody);
        Assert.Equal("idempotent_replay", secondBody.Operation);
        Assert.True(secondBody.IdempotentReplay);
        Assert.Equal(firstBody.TaskId, secondBody.TaskId);
        Assert.Equal(firstBody.ExternalTaskId, secondBody.ExternalTaskId);

        var taskCount = await _factory.CountTasksForUserAsync(owner.UserId);
        Assert.Equal(1, taskCount);
    }

    [Fact]
    public async Task IntegrationEndpoint_EquivalentGuidIdempotencyKeyFormats_ReplayDeterministically()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.replay.formats@example.com");
        var createdCredential = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        var guid = Guid.NewGuid();
        var keyFirstFormat = guid.ToString("B");
        var keySecondFormat = guid.ToString("N");

        var payload = new IntegrationTaskCreateSyncRequest(
            "ext-replay-format-1",
            "Replay format title",
            "Replay format description",
            DateTime.UtcNow.AddDays(1),
            "medium",
            "work",
            false);

        using var firstRequest = BuildCreateSyncRequest(createdCredential.KeyId, createdCredential.Secret, keyFirstFormat, payload);
        var firstResponse = await _client.SendAsync(firstRequest);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstBody);
        Assert.Equal("created", firstBody.Operation);
        Assert.False(firstBody.IdempotentReplay);

        using var secondRequest = BuildCreateSyncRequest(createdCredential.KeyId, createdCredential.Secret, keySecondFormat, payload);
        var secondResponse = await _client.SendAsync(secondRequest);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondBody);
        Assert.Equal("idempotent_replay", secondBody.Operation);
        Assert.True(secondBody.IdempotentReplay);
        Assert.Equal(firstBody.TaskId, secondBody.TaskId);

        var taskCount = await _factory.CountTasksForUserAsync(owner.UserId);
        Assert.Equal(1, taskCount);
    }

    [Fact]
    public async Task IntegrationEndpoint_ConcurrentDuplicateIdempotencyRequests_ApplySingleMutation()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.concurrent.replay@example.com");
        var createdCredential = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);
        var idempotencyKey = Guid.NewGuid().ToString();

        var payload = new IntegrationTaskCreateSyncRequest(
            "ext-concurrent-1",
            "Concurrent title",
            "Concurrent description",
            DateTime.UtcNow.AddDays(2),
            "high",
            "planning",
            true);

        Task<HttpResponseMessage> SendAsync() => _client.SendAsync(
            BuildCreateSyncRequest(createdCredential.KeyId, createdCredential.Secret, idempotencyKey, payload));

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => SendAsync()));
        var bodies = await Task.WhenAll(responses.Select(response => response.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>()));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.All(bodies, body => Assert.NotNull(body));

        var resolvedBodies = bodies!;
        var distinctTaskIds = resolvedBodies.Select(body => body!.TaskId).Distinct().Count();
        Assert.Equal(1, distinctTaskIds);

        var replayCount = resolvedBodies.Count(body => body!.IdempotentReplay);
        Assert.True(replayCount >= 1);

        var taskCount = await _factory.CountTasksForUserAsync(owner.UserId);
        Assert.Equal(1, taskCount);
    }

    [Fact]
    public async Task IntegrationEndpoint_DifferentIdempotencyKeys_ForSameExternalTaskId_AllowNormalUpdateFlow()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.idempotency.diffkeys@example.com");
        var createdCredential = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        using var firstRequest = BuildCreateSyncRequest(
            createdCredential.KeyId,
            createdCredential.Secret,
            Guid.NewGuid().ToString(),
            new IntegrationTaskCreateSyncRequest(
                "ext-diff-keys-1",
                "First title",
                "first",
                DateTime.UtcNow.AddDays(1),
                "low",
                "home",
                false));

        var firstResponse = await _client.SendAsync(firstRequest);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstBody);
        Assert.Equal("created", firstBody.Operation);
        Assert.False(firstBody.IdempotentReplay);

        using var secondRequest = BuildCreateSyncRequest(
            createdCredential.KeyId,
            createdCredential.Secret,
            Guid.NewGuid().ToString(),
            new IntegrationTaskCreateSyncRequest(
                "ext-diff-keys-1",
                "Second title",
                "second",
                DateTime.UtcNow.AddDays(5),
                "medium",
                "work",
                true));

        var secondResponse = await _client.SendAsync(secondRequest);
        var secondBody = await secondResponse.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.NotNull(secondBody);
        Assert.Equal("updated", secondBody.Operation);
        Assert.False(secondBody.IdempotentReplay);
        Assert.Equal(firstBody.TaskId, secondBody.TaskId);
    }

    [Fact]
    public async Task IntegrationEndpoint_SameExternalTaskIdAcrossOwners_DoesNotCrossMutate()
    {
        var ownerA = await RegisterAndLoginWithUserAsync("integration.owner.a.isolation@example.com");
        var ownerB = await RegisterAndLoginWithUserAsync("integration.owner.b.isolation@example.com");

        var credentialA = await CreateCredentialAsync(ownerA.Tokens.AccessToken, ["tasks:create-sync"]);
        var credentialB = await CreateCredentialAsync(ownerB.Tokens.AccessToken, ["tasks:create-sync"]);

        using var requestA = BuildCreateSyncRequest(
            credentialA.KeyId,
            credentialA.Secret,
            Guid.NewGuid().ToString(),
            new IntegrationTaskCreateSyncRequest(
                "shared-external-id",
                "Owner A task",
                "A",
                DateTime.UtcNow.AddDays(1),
                "medium",
                "work",
                false));

        var responseA = await _client.SendAsync(requestA);
        var payloadA = await responseA.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, responseA.StatusCode);
        Assert.NotNull(payloadA);

        using var requestB = BuildCreateSyncRequest(
            credentialB.KeyId,
            credentialB.Secret,
            Guid.NewGuid().ToString(),
            new IntegrationTaskCreateSyncRequest(
                "shared-external-id",
                "Owner B task",
                "B",
                DateTime.UtcNow.AddDays(2),
                "low",
                "home",
                false));

        var responseB = await _client.SendAsync(requestB);
        var payloadB = await responseB.Content.ReadFromJsonAsync<IntegrationTaskCreateSyncResponse>();

        Assert.Equal(HttpStatusCode.OK, responseB.StatusCode);
        Assert.NotNull(payloadB);
        Assert.NotEqual(payloadA.TaskId, payloadB.TaskId);

        var persistedTaskA = await _factory.FindTaskByIdAsync(payloadA.TaskId);
        var persistedTaskB = await _factory.FindTaskByIdAsync(payloadB.TaskId);

        Assert.NotNull(persistedTaskA);
        Assert.NotNull(persistedTaskB);
        Assert.Equal(ownerA.UserId, persistedTaskA.UserId);
        Assert.Equal(ownerB.UserId, persistedTaskB.UserId);
        Assert.Equal("Owner A task", persistedTaskA.Title);
        Assert.Equal("Owner B task", persistedTaskB.Title);
    }

    [Fact]
    public async Task IntegrationEndpoint_WithInvalidTaskPayload_ReturnsValidationProblemDetails()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.validation@example.com");
        var createdCredential = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        using var request = BuildCreateSyncRequest(
            createdCredential.KeyId,
            createdCredential.Secret,
            Guid.NewGuid().ToString(),
            new IntegrationTaskCreateSyncRequest(
                "",
                "",
                new string('d', 2200),
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local),
                "urgent",
                "",
                false));

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        var errorFields = json.RootElement.GetProperty("errors")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", root.GetProperty("type").GetString());
        Assert.Equal("validation.request.invalid", root.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("traceId").GetString()));
        Assert.Contains("externalTaskId", errorFields);
        Assert.Contains("title", errorFields);
        Assert.Contains("priority", errorFields);
        Assert.Contains("category", errorFields);
        Assert.Contains("dueAtUtc", errorFields);
    }

    [Fact]
    public async Task IntegrationEndpoint_WithMissingIdempotencyKey_ReturnsValidationProblemDetails()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.missing.idempotency@example.com");
        var createdCredential = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/tasks/create-sync");
        request.Headers.Add("X-Integration-Key-Id", createdCredential.KeyId);
        request.Headers.Add("X-Integration-Secret", createdCredential.Secret);
        request.Content = JsonContent.Create(new IntegrationTaskCreateSyncRequest(
            "ext-missing-idempotency",
            "Task",
            "desc",
            DateTime.UtcNow.AddDays(1),
            "medium",
            "work",
            false));

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var errors = json.RootElement.GetProperty("errors")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation.request.invalid", json.RootElement.GetProperty("code").GetString());
        Assert.Contains("idempotencyKey", errors);
    }

    [Fact]
    public async Task IntegrationEndpoint_WithInvalidIdempotencyKey_ReturnsValidationProblemDetails()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.invalid.idempotency@example.com");
        var createdCredential = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/tasks/create-sync");
        request.Headers.Add("X-Integration-Key-Id", createdCredential.KeyId);
        request.Headers.Add("X-Integration-Secret", createdCredential.Secret);
        request.Headers.Add("Idempotency-Key", "not-a-guid");
        request.Content = JsonContent.Create(new IntegrationTaskCreateSyncRequest(
            "ext-invalid-idempotency",
            "Task",
            "desc",
            DateTime.UtcNow.AddDays(1),
            "medium",
            "work",
            false));

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var errors = json.RootElement.GetProperty("errors")
            .EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation.request.invalid", json.RootElement.GetProperty("code").GetString());
        Assert.Contains("idempotencyKey", errors);

        var failureEvent = await _factory.FindLatestIntegrationFailureEventAsync(owner.UserId, "automation-suite");
        Assert.NotNull(failureEvent);
        Assert.Equal("validation", failureEvent!.ErrorClass);
        Assert.Equal("validation.request.invalid", failureEvent.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, (HttpStatusCode)failureEvent.HttpStatus);
        Assert.Equal("not-a-guid", failureEvent.IdempotencyKey);
    }

    [Fact]
    public async Task IntegrationEndpoint_ValidationFailure_IncludesErrorClassAndRecoveryHint()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.validation.classification@example.com");
        var createdCredential = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        using var request = BuildCreateSyncRequest(
            createdCredential.KeyId,
            createdCredential.Secret,
            Guid.NewGuid().ToString(),
            new IntegrationTaskCreateSyncRequest(
                "",
                "",
                "invalid",
                DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Local),
                "invalid-priority",
                "",
                false));

        var response = await _client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validation", payload.GetProperty("errorClass").GetString());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.Contains("retry", payload.GetProperty("recovery").GetString(), StringComparison.OrdinalIgnoreCase);

        var failureEvent = await _factory.FindLatestIntegrationFailureEventAsync(owner.UserId, "automation-suite");
        Assert.NotNull(failureEvent);
        Assert.Equal("validation", failureEvent!.ErrorClass);
    }

    [Fact]
    public async Task IntegrationEndpoint_ForbiddenInRepository_PersistsAuthorizationFailureEvent()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.forbidden.repo@example.com");
        var otherUser = await RegisterAndLoginWithUserAsync("integration.owner.forbidden.repo.other@example.com");
        var createdCredential = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        var otherTaskId = Guid.NewGuid();
        await _factory.AddTaskAsync(new TaskItem
        {
            Id = otherTaskId,
            UserId = otherUser.UserId,
            Title = "Other owner task",
            Description = "seed",
            DueAtUtc = null,
            Priority = "medium",
            Category = "work",
            IsCompleted = false,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        });

        await _factory.AddIntegrationTaskSyncBindingAsync(new IntegrationTaskSyncBinding
        {
            Id = Guid.NewGuid(),
            OwnerUserId = owner.UserId,
            IntegrationId = "automation-suite",
            ExternalTaskId = "ext-forbidden-1",
            TaskId = otherTaskId,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5)
        });

        using var request = BuildCreateSyncRequest(
            createdCredential.KeyId,
            createdCredential.Secret,
            Guid.NewGuid().ToString(),
            new IntegrationTaskCreateSyncRequest(
                "ext-forbidden-1",
                "Title",
                "Description",
                DateTime.UtcNow.AddDays(1),
                "medium",
                "work",
                false));

        var response = await _client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("authorization", payload.GetProperty("errorClass").GetString());
        Assert.Equal("auth.forbidden", payload.GetProperty("code").GetString());

        var failureEvent = await _factory.FindLatestIntegrationFailureEventAsync(owner.UserId, "automation-suite");
        Assert.NotNull(failureEvent);
        Assert.Equal("authorization", failureEvent!.ErrorClass);
        Assert.Equal("auth.forbidden", failureEvent.ErrorCode);
        Assert.Equal("ext-forbidden-1", failureEvent.ExternalTaskId);
    }

    [Fact]
    public async Task IntegrationEndpoint_WithMissingScope_ReturnsForbiddenProblemDetails()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.missingscope@example.com");

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/credentials");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", owner.Tokens.AccessToken);
        createRequest.Content = JsonContent.Create(new CreateIntegrationCredentialRequest(
            "automation-suite",
            "Automation Suite",
            ["tasks:read-only"],
            DateTime.UtcNow.AddDays(30)));

        var createResponse = await _client.SendAsync(createRequest);
        var createPayload = await createResponse.Content.ReadFromJsonAsync<IntegrationCredentialCreatedResponse>();
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createPayload);

        using var integrationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/tasks/create-sync");
        integrationRequest.Headers.Add("X-Integration-Key-Id", createPayload.KeyId);
        integrationRequest.Headers.Add("X-Integration-Secret", createPayload.Secret);
        integrationRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        integrationRequest.Content = JsonContent.Create(new IntegrationTaskCreateSyncRequest(
            "ext-2",
            "Sync Task",
            "Scoped integration test",
            DateTime.UtcNow.AddDays(2),
            "medium",
            "work",
            false));

        var integrationResponse = await _client.SendAsync(integrationRequest);
        var payload = await integrationResponse.Content.ReadFromJsonAsync<ProblemDetailsPayload>();

        Assert.Equal(HttpStatusCode.Forbidden, integrationResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.Type);
        Assert.Equal("auth.integration.scope.denied", payload.Code);
        Assert.False(string.IsNullOrWhiteSpace(payload.TraceId));
    }

    [Fact]
    public async Task IntegrationEndpoint_WithRevokedCredential_ReturnsUnauthorized()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.revoked@example.com");
        var created = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        await _factory.RevokeIntegrationCredentialAsync(created.CredentialId);

        using var integrationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/tasks/create-sync");
        integrationRequest.Headers.Add("X-Integration-Key-Id", created.KeyId);
        integrationRequest.Headers.Add("X-Integration-Secret", created.Secret);
        integrationRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        integrationRequest.Content = JsonContent.Create(new IntegrationTaskCreateSyncRequest(
            "ext-3",
            "Sync Task",
            "Revoked integration test",
            DateTime.UtcNow.AddDays(2),
            "medium",
            "work",
            false));

        var integrationResponse = await _client.SendAsync(integrationRequest);
        var payload = await integrationResponse.Content.ReadFromJsonAsync<ProblemDetailsPayload>();

        Assert.Equal(HttpStatusCode.Unauthorized, integrationResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("auth.integration.revoked", payload.Code);
    }

    [Fact]
    public async Task IntegrationEndpoint_WithExpiredCredential_ReturnsUnauthorized()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.expired@example.com");
        var created = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        await _factory.SetIntegrationCredentialExpiryAsync(created.CredentialId, DateTime.UtcNow.AddMinutes(-5));

        using var integrationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/tasks/create-sync");
        integrationRequest.Headers.Add("X-Integration-Key-Id", created.KeyId);
        integrationRequest.Headers.Add("X-Integration-Secret", created.Secret);
        integrationRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        integrationRequest.Content = JsonContent.Create(new IntegrationTaskCreateSyncRequest(
            "ext-4",
            "Sync Task",
            "Expired integration test",
            DateTime.UtcNow.AddDays(2),
            "medium",
            "work",
            false));

        var integrationResponse = await _client.SendAsync(integrationRequest);
        var payload = await integrationResponse.Content.ReadFromJsonAsync<ProblemDetailsPayload>();

        Assert.Equal(HttpStatusCode.Unauthorized, integrationResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("auth.integration.expired", payload.Code);
    }

    [Fact]
    public async Task IntegrationEndpoint_WithInvalidSecret_ReturnsUnauthorizedWithoutSecretLeak()
    {
        var owner = await RegisterAndLoginWithUserAsync("integration.owner.invalid@example.com");
        var created = await CreateCredentialAsync(owner.Tokens.AccessToken, ["tasks:create-sync"]);

        using var integrationRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/tasks/create-sync");
        integrationRequest.Headers.Add("X-Integration-Key-Id", created.KeyId);
        integrationRequest.Headers.Add("X-Integration-Secret", "totally-invalid-secret");
        integrationRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        integrationRequest.Content = JsonContent.Create(new IntegrationTaskCreateSyncRequest(
            "ext-5",
            "Sync Task",
            "Invalid secret integration test",
            DateTime.UtcNow.AddDays(2),
            "medium",
            "work",
            false));

        var integrationResponse = await _client.SendAsync(integrationRequest);
        var body = await integrationResponse.Content.ReadAsStringAsync();
        var payload = await integrationResponse.Content.ReadFromJsonAsync<ProblemDetailsPayload>();

        Assert.Equal(HttpStatusCode.Unauthorized, integrationResponse.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("auth.integration.invalid", payload.Code);
        Assert.DoesNotContain(created.Secret, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokeCredential_FromDifferentOwner_ReturnsNotFoundAndDoesNotLeak()
    {
        var ownerA = await RegisterAndLoginWithUserAsync("integration.owner.a@example.com");
        var ownerB = await RegisterAndLoginWithUserAsync("integration.owner.b@example.com");
        var created = await CreateCredentialAsync(ownerA.Tokens.AccessToken, ["tasks:create-sync"]);

        using var revokeRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/integrations/credentials/{created.CredentialId}");
        revokeRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerB.Tokens.AccessToken);

        var revokeResponse = await _client.SendAsync(revokeRequest);
        var revokePayload = await revokeResponse.Content.ReadFromJsonAsync<ProblemDetailsPayload>();

        Assert.Equal(HttpStatusCode.NotFound, revokeResponse.StatusCode);
        Assert.NotNull(revokePayload);
        Assert.Equal("integrations.credential.not_found", revokePayload.Code);

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/integrations/credentials");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerB.Tokens.AccessToken);

        var listResponse = await _client.SendAsync(listRequest);
        var listPayload = await listResponse.Content.ReadFromJsonAsync<IntegrationCredentialListResponse>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(listPayload);
        Assert.DoesNotContain(listPayload.Credentials, item => item.CredentialId == created.CredentialId);
    }

    private async Task<(Guid UserId, LoginResponse Tokens)> RegisterAndLoginWithUserAsync(string email)
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass123!"));
        var registerPayload = (await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>())!;

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "StrongPass123!"));
        var loginPayload = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;

        return (registerPayload.UserId, loginPayload);
    }

    private async Task<IntegrationCredentialCreatedResponse> CreateCredentialAsync(string accessToken, string[] scopes)
    {
        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/credentials");
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        createRequest.Content = JsonContent.Create(new CreateIntegrationCredentialRequest(
            "automation-suite",
            "Automation Suite",
            scopes,
            DateTime.UtcNow.AddDays(30)));

        var response = await _client.SendAsync(createRequest);
        var payload = await response.Content.ReadFromJsonAsync<IntegrationCredentialCreatedResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(payload);

        return payload;
    }

    private static HttpRequestMessage BuildCreateSyncRequest(
        string keyId,
        string secret,
        string idempotencyKey,
        IntegrationTaskCreateSyncRequest payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/tasks/create-sync");
        request.Headers.Add("X-Integration-Key-Id", keyId);
        request.Headers.Add("X-Integration-Secret", secret);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(payload);
        return request;
    }
}
