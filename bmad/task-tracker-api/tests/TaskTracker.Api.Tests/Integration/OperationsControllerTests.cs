using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Features.Integrations.Contracts;
using TaskTracker.Api.Features.Tasks.Contracts;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Tests.Integration;

public class OperationsControllerTests
{
    [Fact]
    public async Task GetSuspiciousCases_WithAdminRole_ReturnsDeterministicCasesAndPrivacySafePayload()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var adminUserId = await RegisterUserAsync(client, "ops.admin@example.com");
        await factory.SetUserRoleAsync(adminUserId, AppRoles.Admin);
        var adminTokens = await LoginAsync(client, "ops.admin@example.com");

        var spikeUserId = await RegisterUserAsync(client, "ops.spike@example.com");
        var mismatchUserId = await RegisterUserAsync(client, "ops.mismatch@example.com");

        await factory.SetLeaderboardParticipationModeAsync(spikeUserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetLeaderboardParticipationModeAsync(mismatchUserId, LeaderboardParticipationMode.Public);
        await factory.SetUserDisplayNameAsync(mismatchUserId, "Mismatch User");

        await AddCompletedTasksAsync(factory, spikeUserId, 5, DateTime.UtcNow.AddMinutes(-5));
        await AddCompletedTasksAsync(factory, mismatchUserId, 12, DateTime.UtcNow.AddDays(-8));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/admin/suspicious-cases?page=1&pageSize=10");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(10, payload.GetProperty("pageSize").GetInt32());

        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.True(items.Length >= 2);

        var first = items[0];
        var second = items[1];

        Assert.True(first.GetProperty("severity").GetInt32() >= second.GetProperty("severity").GetInt32());
        Assert.Equal("rankingMismatch", first.GetProperty("anomalyType").GetString());
        Assert.Equal("Mismatch User", first.GetProperty("publicIdentity").GetString());
        Assert.Equal("public", first.GetProperty("identityMode").GetString());

        Assert.Equal("activitySpike", second.GetProperty("anomalyType").GetString());
        Assert.Equal("anonymous", second.GetProperty("identityMode").GetString());
        Assert.StartsWith("anon-", second.GetProperty("publicIdentity").GetString(), StringComparison.Ordinal);

        Assert.False(first.TryGetProperty("email", out _));
        Assert.False(first.TryGetProperty("userId", out _));
        Assert.False(first.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task GetSuspiciousCases_WithNonAdminRole_ReturnsForbiddenProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var userId = await RegisterUserAsync(client, "ops.user@example.com");
        var userTokens = await LoginAsync(client, "ops.user@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/admin/suspicious-cases");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.GetProperty("type").GetString());
        Assert.Equal("Forbidden", payload.GetProperty("title").GetString());
        Assert.Equal("authz.access.denied", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task GetSuspiciousCases_WithFilterAndPagination_ReturnsBoundedResultPage()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var adminUserId = await RegisterUserAsync(client, "ops.filter.admin@example.com");
        await factory.SetUserRoleAsync(adminUserId, AppRoles.Admin);
        var adminTokens = await LoginAsync(client, "ops.filter.admin@example.com");

        var firstSpikeUserId = await RegisterUserAsync(client, "ops.filter.spike1@example.com");
        var secondSpikeUserId = await RegisterUserAsync(client, "ops.filter.spike2@example.com");

        await factory.SetLeaderboardParticipationModeAsync(firstSpikeUserId, LeaderboardParticipationMode.Anonymous);
        await factory.SetLeaderboardParticipationModeAsync(secondSpikeUserId, LeaderboardParticipationMode.Anonymous);

        await AddCompletedTasksAsync(factory, firstSpikeUserId, 6, DateTime.UtcNow.AddMinutes(-30));
        await AddCompletedTasksAsync(factory, secondSpikeUserId, 5, DateTime.UtcNow.AddMinutes(-15));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/ops/admin/suspicious-cases?anomalyType=activitySpike&page=1&pageSize=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(1, payload.GetProperty("pageSize").GetInt32());
        Assert.True(payload.GetProperty("totalCount").GetInt32() >= 2);
        Assert.True(payload.GetProperty("hasNextPage").GetBoolean());

        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal("activitySpike", items[0].GetProperty("anomalyType").GetString());
    }

    [Fact]
    public async Task GetSuspiciousCases_WritesStructuredLogWithActorAndTraceId()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var adminUserId = await RegisterUserAsync(client, "ops.logs.admin@example.com");
        await factory.SetUserRoleAsync(adminUserId, AppRoles.Admin);
        var adminTokens = await LoginAsync(client, "ops.logs.admin@example.com");

        factory.ClearCapturedLogs();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/admin/suspicious-cases");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var logs = factory.GetCapturedLogs();
        Assert.Contains(logs, entry =>
            entry.Category.Contains("OperationsController", StringComparison.Ordinal)
            && entry.Message.Contains("Suspicious-case review served", StringComparison.Ordinal)
            && entry.Message.Contains("ActorId", StringComparison.Ordinal)
            && entry.Message.Contains("TraceId", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApplyModerationAction_WithAdminRoleAndConfirmation_AppliesRankingCorrectionAndWritesAudit()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var adminUserId = await RegisterUserAsync(client, "ops.mod.admin@example.com");
        await factory.SetUserRoleAsync(adminUserId, AppRoles.Admin);
        var adminTokens = await LoginAsync(client, "ops.mod.admin@example.com");

        var targetUserId = await RegisterUserAsync(client, "ops.mod.target@example.com");
        await factory.SetLeaderboardParticipationModeAsync(targetUserId, LeaderboardParticipationMode.Public);
        await AddCompletedTasksAsync(factory, targetUserId, 11, DateTime.UtcNow.AddMinutes(-10));

        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/admin/suspicious-cases?anomalyType=rankingMismatch&page=1&pageSize=10");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);
        var listResponse = await client.SendAsync(listRequest);
        var listPayload = (await listResponse.Content.ReadFromJsonAsync<JsonElement>())!;
        var targetCase = listPayload.GetProperty("items").EnumerateArray()
            .First(item => item.GetProperty("caseId").GetString() == $"ranking-mismatch-{targetUserId:N}");

        var caseId = targetCase.GetProperty("caseId").GetString()!;
        var confirmationToken = targetCase.GetProperty("destructiveConfirmationToken").GetString();

        using var moderationRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/ops/admin/suspicious-cases/{caseId}/moderation-actions");
        moderationRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);
        moderationRequest.Content = JsonContent.Create(new
        {
            actionType = "rankingCorrection",
            reasonCode = "manual-investigation-confirmed",
            reasonText = "Applied after manual ops review.",
            confirmDestructive = true,
            confirmationToken
        });

        var moderationResponse = await client.SendAsync(moderationRequest);
        var moderationPayload = (await moderationResponse.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, moderationResponse.StatusCode);
        Assert.Equal("rankingCorrection", moderationPayload.GetProperty("actionType").GetString());
        Assert.Equal("succeeded", moderationPayload.GetProperty("outcome").GetString());
        Assert.False(string.IsNullOrWhiteSpace(moderationPayload.GetProperty("traceId").GetString()));

        var participationMode = await factory.GetLeaderboardParticipationModeAsync(targetUserId);
        Assert.Equal(LeaderboardParticipationMode.Hidden, participationMode);

        var auditCount = await factory.CountModerationActionAuditsAsync(targetUserId);
        Assert.Equal(1, auditCount);

        var latestAudit = await factory.FindLatestModerationAuditByCaseIdAsync(caseId);
        Assert.NotNull(latestAudit);
        Assert.Equal("manual-investigation-confirmed", latestAudit!.ReasonCode);
        Assert.Equal("succeeded", latestAudit.Outcome);
        Assert.Equal(caseId, latestAudit.CaseId);
        Assert.False(string.IsNullOrWhiteSpace(latestAudit.TraceId));

        Assert.Equal(1, await factory.CountPrivilegedActionAuditsAsync(targetUserId));
        var privilegedAudit = await factory.FindLatestPrivilegedActionAuditByTargetUserIdAsync(targetUserId);
        Assert.NotNull(privilegedAudit);
        Assert.Equal("moderation.apply", privilegedAudit!.ActionType);
        Assert.Equal("manual-investigation-confirmed", privilegedAudit.ReasonCode);
        Assert.Equal("succeeded", privilegedAudit.Outcome);
        Assert.False(string.IsNullOrWhiteSpace(privilegedAudit.CorrelationId));
        Assert.False(string.IsNullOrWhiteSpace(privilegedAudit.TraceId));
    }

    [Fact]
    public async Task ApplyModerationAction_WithNonAdminRole_ReturnsForbiddenProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var userId = await RegisterUserAsync(client, "ops.mod.nonadmin@example.com");
        var userTokens = await LoginAsync(client, "ops.mod.nonadmin@example.com");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/ops/admin/suspicious-cases/ranking-mismatch-{userId:N}/moderation-actions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userTokens.AccessToken);
        request.Content = JsonContent.Create(new
        {
            actionType = "flagEntity",
            reasonCode = "suspicious-ranking-signal",
            reasonText = "Policy check.",
            confirmDestructive = false,
            confirmationToken = (string?)null
        });

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.GetProperty("type").GetString());
        Assert.Equal("authz.access.denied", payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ApplyModerationAction_WithoutDestructiveConfirmation_ReturnsConfirmationRequired()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var adminUserId = await RegisterUserAsync(client, "ops.mod.confirm.admin@example.com");
        await factory.SetUserRoleAsync(adminUserId, AppRoles.Admin);
        var adminTokens = await LoginAsync(client, "ops.mod.confirm.admin@example.com");

        var targetUserId = await RegisterUserAsync(client, "ops.mod.confirm.target@example.com");
        await AddCompletedTasksAsync(factory, targetUserId, 10, DateTime.UtcNow.AddMinutes(-6));

        var caseId = $"ranking-mismatch-{targetUserId:N}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/ops/admin/suspicious-cases/{caseId}/moderation-actions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);
        request.Content = JsonContent.Create(new
        {
            actionType = "rankingCorrection",
            reasonCode = "manual-investigation-confirmed",
            reasonText = "missing confirmation",
            confirmDestructive = false,
            confirmationToken = (string?)null
        });

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/confirmation-required", payload.GetProperty("type").GetString());
        Assert.Equal("ops.moderation.confirmation_required", payload.GetProperty("code").GetString());
        Assert.Contains("confirm", payload.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        var auditCount = await factory.CountModerationActionAuditsAsync(targetUserId);
        Assert.Equal(0, auditCount);
    }

    [Fact]
    public async Task ApplyModerationAction_FlagEntity_IsRetrySafeAndDeterministic()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var adminUserId = await RegisterUserAsync(client, "ops.mod.retry.admin@example.com");
        await factory.SetUserRoleAsync(adminUserId, AppRoles.Admin);
        var adminTokens = await LoginAsync(client, "ops.mod.retry.admin@example.com");

        var targetUserId = await RegisterUserAsync(client, "ops.mod.retry.target@example.com");
        await AddCompletedTasksAsync(factory, targetUserId, 5, DateTime.UtcNow.AddMinutes(-2));

        var caseId = $"activity-spike-{targetUserId:N}";
        var requestBody = new
        {
            actionType = "flagEntity",
            reasonCode = "abuse-prevention-policy",
            reasonText = "retry-safe command",
            confirmDestructive = false,
            confirmationToken = (string?)null
        };

        using var firstRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/ops/admin/suspicious-cases/{caseId}/moderation-actions");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);
        firstRequest.Content = JsonContent.Create(requestBody);

        var firstResponse = await client.SendAsync(firstRequest);
        var firstPayload = (await firstResponse.Content.ReadFromJsonAsync<JsonElement>())!;

        using var secondRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/ops/admin/suspicious-cases/{caseId}/moderation-actions");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminTokens.AccessToken);
        secondRequest.Content = JsonContent.Create(requestBody);

        var secondResponse = await client.SendAsync(secondRequest);
        var secondPayload = (await secondResponse.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal("succeeded", firstPayload.GetProperty("outcome").GetString());
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Equal("alreadyApplied", secondPayload.GetProperty("outcome").GetString());

        Assert.True(await factory.IsUserSuspiciousFlaggedAsync(targetUserId));
        Assert.Equal(1, await factory.CountModerationActionAuditsAsync(targetUserId));
    }

    [Fact]
    public async Task GetSupportUserSnapshot_WithSupportRole_ReturnsConsolidatedReadOnlyDiagnostics()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var targetUserId = await RegisterUserAsync(client, "ops.support.target@example.com");
        var supportUserId = await RegisterUserAsync(client, "ops.support.viewer@example.com");
        await factory.SetUserRoleAsync(supportUserId, AppRoles.Support);
        await factory.SetUserDisplayNameAsync(targetUserId, "Support Target");

        var supportTokens = await LoginAsync(client, "ops.support.viewer@example.com");

        var completedTask = await factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            Title = "Completed support task",
            Description = "Fixture task",
            DueAtUtc = null,
            Priority = "medium",
            Category = "work",
            IsCompleted = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-1)
        });

        await factory.AddTaskCompletionEventAsync(new TaskCompletionEvent
        {
            Id = Guid.NewGuid(),
            TaskId = completedTask.Id,
            OwnerId = targetUserId,
            EventName = "TaskCompleted",
            ResultingIsCompleted = true,
            IdempotencyKey = $"support-completion-{completedTask.Id:N}",
            OccurredAtUtc = DateTime.UtcNow.AddHours(-2),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
        });

        await factory.AddXpLedgerEntryAsync(new XpLedgerEntry
        {
            Id = Guid.NewGuid(),
            OwnerId = targetUserId,
            TaskId = completedTask.Id,
            TaskCompletionEventId = Guid.NewGuid(),
            EventName = "TaskCompleted",
            IdempotencyKey = $"support-xp-{completedTask.Id:N}",
            XpGranted = 25,
            OccurredAtUtc = DateTime.UtcNow.AddHours(-1),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-1)
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/ops/support/users/{targetUserId}?windowDays=30&markerLimit=3");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportTokens.AccessToken);
        request.Headers.Add("X-Correlation-Id", "support-diag-corr-1");

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(targetUserId, payload.GetProperty("account").GetProperty("userId").GetGuid());
        Assert.Equal("Support Target", payload.GetProperty("account").GetProperty("displayName").GetString());
        Assert.Equal(1, payload.GetProperty("taskState").GetProperty("totalCount").GetInt32());
        Assert.Equal(1, payload.GetProperty("taskState").GetProperty("completedCount").GetInt32());
        Assert.Equal(25, payload.GetProperty("xpState").GetProperty("totalXp").GetInt32());
        Assert.Equal("support-diag-corr-1", payload.GetProperty("correlationId").GetString());

        var markers = payload.GetProperty("recentMarkers").EnumerateArray().ToArray();
        Assert.True(markers.Length <= 3);
        Assert.True(markers.Length >= 2);

        for (var index = 1; index < markers.Length; index++)
        {
            var previous = markers[index - 1].GetProperty("occurredAtUtc").GetDateTime();
            var current = markers[index].GetProperty("occurredAtUtc").GetDateTime();
            Assert.True(previous >= current);
        }

        Assert.False(payload.GetProperty("account").TryGetProperty("passwordHash", out _));
        Assert.False(payload.TryGetProperty("passwordHash", out _));
    }

    [Fact]
    public async Task GetSupportUserSnapshot_WithNonSupportRole_ReturnsForbiddenProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var targetUserId = await RegisterUserAsync(client, "ops.support.nonrole.target@example.com");
        var standardTokens = await LoginAsync(client, "ops.support.nonrole.target@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/ops/support/users/{targetUserId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", standardTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.GetProperty("type").GetString());
        Assert.Equal("authz.access.denied", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task GetSupportUserSnapshot_WithInvalidQuery_ReturnsValidationProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var targetUserId = await RegisterUserAsync(client, "ops.support.validation.target@example.com");
        var supportUserId = await RegisterUserAsync(client, "ops.support.validation.viewer@example.com");
        await factory.SetUserRoleAsync(supportUserId, AppRoles.Support);
        var supportTokens = await LoginAsync(client, "ops.support.validation.viewer@example.com");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/ops/support/users/{targetUserId}?windowDays=0&markerLimit=100");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.True(payload.GetProperty("errors").TryGetProperty("windowDays", out _));
        Assert.True(payload.GetProperty("errors").TryGetProperty("markerLimit", out _));
    }

    [Fact]
    public async Task GetSupportUserTimeline_WithSupportRole_ReturnsFilteredDeterministicTimelinePage()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var targetUserId = await RegisterUserAsync(client, "ops.timeline.target@example.com");
        var supportUserId = await RegisterUserAsync(client, "ops.timeline.support@example.com");
        await factory.SetUserRoleAsync(supportUserId, AppRoles.Support);
        var supportTokens = await LoginAsync(client, "ops.timeline.support@example.com");

        var task = await factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            Title = "Timeline task",
            Description = "fixture",
            DueAtUtc = null,
            Priority = "medium",
            Category = "work",
            IsCompleted = true,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-2)
        });

        var firstAt = DateTime.UtcNow.AddHours(-4);
        var secondAt = DateTime.UtcNow.AddHours(-2);

        await factory.AddXpLedgerEntryAsync(new XpLedgerEntry
        {
            Id = Guid.NewGuid(),
            OwnerId = targetUserId,
            TaskId = task.Id,
            TaskCompletionEventId = Guid.NewGuid(),
            EventName = "TaskCompleted",
            IdempotencyKey = "timeline-xp-1",
            XpGranted = 20,
            OccurredAtUtc = firstAt,
            CreatedAtUtc = firstAt
        });

        await factory.AddXpLedgerEntryAsync(new XpLedgerEntry
        {
            Id = Guid.NewGuid(),
            OwnerId = targetUserId,
            TaskId = task.Id,
            TaskCompletionEventId = Guid.NewGuid(),
            EventName = "TaskCompleted",
            IdempotencyKey = "timeline-xp-2",
            XpGranted = 15,
            OccurredAtUtc = secondAt,
            CreatedAtUtc = secondAt
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/ops/support/users/{targetUserId}/timeline?eventType=xpLedger&page=1&maxItems=1&startUtc={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-7).ToString("O"))}&endUtc={Uri.EscapeDataString(DateTime.UtcNow.ToString("O"))}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportTokens.AccessToken);
        request.Headers.Add("X-Correlation-Id", "support-timeline-corr-1");

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(1, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, payload.GetProperty("totalCount").GetInt32());
        Assert.True(payload.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal("support-timeline-corr-1", payload.GetProperty("correlationId").GetString());
        Assert.Equal("xpLedger", payload.GetProperty("filters").GetProperty("eventType").GetString());

        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal("xpLedger", items[0].GetProperty("eventType").GetString());
        Assert.Equal("progress.xp.recorded", items[0].GetProperty("messageCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(items[0].GetProperty("correlationId").GetString()));
    }

    [Fact]
    public async Task GetSupportUserTimeline_WithNonSupportRole_ReturnsForbiddenProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var targetUserId = await RegisterUserAsync(client, "ops.timeline.forbidden.target@example.com");
        var standardTokens = await LoginAsync(client, "ops.timeline.forbidden.target@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/ops/support/users/{targetUserId}/timeline");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", standardTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.GetProperty("type").GetString());
        Assert.Equal("authz.access.denied", payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetSupportUserTimeline_WithInvalidDateRange_ReturnsValidationProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var targetUserId = await RegisterUserAsync(client, "ops.timeline.validation.target@example.com");
        var supportUserId = await RegisterUserAsync(client, "ops.timeline.validation.support@example.com");
        await factory.SetUserRoleAsync(supportUserId, AppRoles.Support);
        var supportTokens = await LoginAsync(client, "ops.timeline.validation.support@example.com");

        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(-1);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/ops/support/users/{targetUserId}/timeline?startUtc={Uri.EscapeDataString(start.ToString("O"))}&endUtc={Uri.EscapeDataString(end.ToString("O"))}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.True(payload.GetProperty("errors").TryGetProperty("dateRange", out _));
    }

    [Fact]
    public async Task GetSupportUserTimeline_OrdersByOccurredAtThenStableTieBreaker()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var targetUserId = await RegisterUserAsync(client, "ops.timeline.order.target@example.com");
        var supportUserId = await RegisterUserAsync(client, "ops.timeline.order.support@example.com");
        await factory.SetUserRoleAsync(supportUserId, AppRoles.Support);
        var supportTokens = await LoginAsync(client, "ops.timeline.order.support@example.com");

        var taskId = Guid.NewGuid();
        var sameTimestamp = DateTime.UtcNow.AddHours(-1);

        await factory.AddTaskCompletionEventAsync(new TaskCompletionEvent
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TaskId = taskId,
            OwnerId = targetUserId,
            EventName = "TaskCompleted",
            ResultingIsCompleted = true,
            IdempotencyKey = "timeline-order-completion",
            OccurredAtUtc = sameTimestamp,
            CreatedAtUtc = sameTimestamp
        });

        await factory.AddXpLedgerEntryAsync(new XpLedgerEntry
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            OwnerId = targetUserId,
            TaskId = taskId,
            TaskCompletionEventId = Guid.NewGuid(),
            EventName = "TaskCompleted",
            IdempotencyKey = "timeline-order-xp",
            XpGranted = 25,
            OccurredAtUtc = sameTimestamp,
            CreatedAtUtc = sameTimestamp
        });

        await factory.UpsertStreakSnapshotAsync(new UserStreakSnapshot
        {
            OwnerId = targetUserId,
            Outcome = TaskStreakOutcome.Continue,
            CurrentStreakDays = 4,
            LongestStreakDays = 7,
            TimeZoneId = "UTC",
            EvaluationWindowStartUtc = sameTimestamp.AddDays(-1),
            EvaluationWindowEndUtc = sameTimestamp,
            LastEvaluatedEventId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            LastEvaluationTraceId = "timeline-order-trace",
            LastEvaluatedAtUtc = sameTimestamp
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/ops/support/users/{targetUserId}/timeline?maxItems=10");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
        var items = payload.GetProperty("items").EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(items.Length >= 3);

        for (var index = 1; index < items.Length; index++)
        {
            var previousOccurredAt = items[index - 1].GetProperty("occurredAtUtc").GetDateTime();
            var currentOccurredAt = items[index].GetProperty("occurredAtUtc").GetDateTime();

            if (previousOccurredAt == currentOccurredAt)
            {
                var previousType = items[index - 1].GetProperty("eventType").GetString()!;
                var currentType = items[index].GetProperty("eventType").GetString()!;
                var previousId = items[index - 1].GetProperty("eventId").GetString()!;
                var currentId = items[index].GetProperty("eventId").GetString()!;

                Assert.True(
                    string.CompareOrdinal(previousType, currentType) <= 0
                    || (string.Equals(previousType, currentType, StringComparison.Ordinal)
                        && string.CompareOrdinal(previousId, currentId) <= 0));
            }
            else
            {
                Assert.True(previousOccurredAt >= currentOccurredAt);
            }
        }

        Assert.Contains(items, item => item.GetProperty("eventType").GetString() == "streakEvaluation");
    }

    [Fact]
    public async Task GetPrivilegedAudits_WithSupportRole_ReturnsDeterministicFilteredPage()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var supportUserId = await RegisterUserAsync(client, "ops.priv.audit.support@example.com");
        await factory.SetUserRoleAsync(supportUserId, AppRoles.Support);
        var supportTokens = await LoginAsync(client, "ops.priv.audit.support@example.com");

        var targetUserId = await RegisterUserAsync(client, "ops.priv.audit.target@example.com");
        var now = DateTime.UtcNow;

        await factory.AddPrivilegedActionAuditAsync(new PrivilegedActionAudit
        {
            Id = Guid.NewGuid(),
            ActorUserId = "admin-a",
            ActorRole = AppRoles.Admin,
            TargetUserId = targetUserId,
            ActionType = "moderation.apply",
            ReasonCode = "manual-investigation-confirmed",
            ReasonText = "case closed",
            Outcome = "succeeded",
            OccurredAtUtc = now.AddMinutes(-2),
            CorrelationId = "corr-priv-1",
            TraceId = "trace-priv-1",
            IntentKey = "intent-priv-1"
        });

        await factory.AddPrivilegedActionAuditAsync(new PrivilegedActionAudit
        {
            Id = Guid.NewGuid(),
            ActorUserId = "admin-b",
            ActorRole = AppRoles.Admin,
            TargetUserId = targetUserId,
            ActionType = "moderation.apply",
            ReasonCode = "manual-investigation-confirmed",
            ReasonText = "second row",
            Outcome = "alreadyApplied",
            OccurredAtUtc = now.AddMinutes(-1),
            CorrelationId = "corr-priv-2",
            TraceId = "trace-priv-2",
            IntentKey = "intent-priv-2"
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/ops/admin-support/privileged-audits?targetUserId={targetUserId}&actionType=moderation.apply&page=1&pageSize=1&startUtc={Uri.EscapeDataString(now.AddDays(-7).ToString("O"))}&endUtc={Uri.EscapeDataString(now.ToString("O"))}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportTokens.AccessToken);
        request.Headers.Add("X-Correlation-Id", "audit-query-corr-1");

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(1, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, payload.GetProperty("totalCount").GetInt32());
        Assert.True(payload.GetProperty("hasNextPage").GetBoolean());
        Assert.Equal("audit-query-corr-1", payload.GetProperty("correlationId").GetString());

        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal("moderation.apply", items[0].GetProperty("actionType").GetString());
        Assert.Equal("alreadyApplied", items[0].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task GetPrivilegedAudits_WithNonPrivilegedRole_ReturnsForbiddenProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var userId = await RegisterUserAsync(client, "ops.priv.audit.user@example.com");
        var userTokens = await LoginAsync(client, "ops.priv.audit.user@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/admin-support/privileged-audits");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.GetProperty("type").GetString());
        Assert.Equal("authz.access.denied", payload.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetPrivilegedAudits_WithInvalidDateRange_ReturnsValidationProblemDetailsWithTraceId()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var supportUserId = await RegisterUserAsync(client, "ops.priv.audit.validation@example.com");
        await factory.SetUserRoleAsync(supportUserId, AppRoles.Support);
        var supportTokens = await LoginAsync(client, "ops.priv.audit.validation@example.com");

        var start = DateTime.UtcNow;
        var end = DateTime.UtcNow.AddDays(-1);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/ops/admin-support/privileged-audits?startUtc={Uri.EscapeDataString(start.ToString("O"))}&endUtc={Uri.EscapeDataString(end.ToString("O"))}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/validation", payload.GetProperty("type").GetString());
        Assert.Equal("validation.request.invalid", payload.GetProperty("code").GetString());
        Assert.True(payload.GetProperty("errors").TryGetProperty("dateRange", out _));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task GetIntegrationFailures_WithSupportRole_ReturnsFilteredPaginatedResults()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        var ownerUserId = await RegisterUserAsync(client, "ops.integration.failures.owner@example.com");
        var ownerTokens = await LoginAsync(client, "ops.integration.failures.owner@example.com");

        var supportUserId = await RegisterUserAsync(client, "ops.integration.failures.support@example.com");
        await factory.SetUserRoleAsync(supportUserId, AppRoles.Support);
        var supportTokens = await LoginAsync(client, "ops.integration.failures.support@example.com");

        using var createCredentialRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/credentials");
        createCredentialRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerTokens.AccessToken);
        createCredentialRequest.Content = JsonContent.Create(new CreateIntegrationCredentialRequest(
            "automation-suite",
            "Automation Suite",
            ["tasks:create-sync"],
            DateTime.UtcNow.AddDays(7)));

        var createCredentialResponse = await client.SendAsync(createCredentialRequest);
        var credential = (await createCredentialResponse.Content.ReadFromJsonAsync<IntegrationCredentialCreatedResponse>())!;

        using var failureRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/integrations/tasks/create-sync");
        failureRequest.Headers.Add("X-Integration-Key-Id", credential.KeyId);
        failureRequest.Headers.Add("X-Integration-Secret", credential.Secret);
        failureRequest.Headers.Add("Idempotency-Key", "not-a-guid");
        failureRequest.Headers.Add("X-Correlation-Id", "ops-integration-failures-corr");
        failureRequest.Content = JsonContent.Create(new IntegrationTaskCreateSyncRequest(
            "ext-ops-failure-1",
            "Task",
            "Desc",
            DateTime.UtcNow.AddDays(1),
            "medium",
            "work",
            false));

        var failureResponse = await client.SendAsync(failureRequest);
        Assert.Equal(HttpStatusCode.BadRequest, failureResponse.StatusCode);

        using var queryRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/ops/admin-support/integration-failures?ownerUserId={ownerUserId}&integrationId=automation-suite&errorClass=validation&page=1&pageSize=10");
        queryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", supportTokens.AccessToken);
        queryRequest.Headers.Add("X-Correlation-Id", "ops-query-correlation-1");

        var queryResponse = await client.SendAsync(queryRequest);
        var payload = (await queryResponse.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(10, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal("ops-query-correlation-1", payload.GetProperty("correlationId").GetString());

        var items = payload.GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(items);
        Assert.Equal("validation", items[0].GetProperty("errorClass").GetString());
        Assert.Equal("validation.request.invalid", items[0].GetProperty("errorCode").GetString());
        Assert.Equal("ops-integration-failures-corr", items[0].GetProperty("correlationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(items[0].GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task GetIntegrationFailures_WithNonPrivilegedRole_ReturnsForbiddenProblemDetails()
    {
        await using var factory = new AuthTestFactory();
        using var client = factory.CreateClient();

        await RegisterUserAsync(client, "ops.integration.failures.user@example.com");
        var userTokens = await LoginAsync(client, "ops.integration.failures.user@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/ops/admin-support/integration-failures");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userTokens.AccessToken);

        var response = await client.SendAsync(request);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>())!;

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.GetProperty("type").GetString());
        Assert.Equal("authz.access.denied", payload.GetProperty("code").GetString());
    }

    private static async Task<Guid> RegisterUserAsync(HttpClient client, string email)
    {
        var registerResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new RegisterRequest(email, "StrongPass123!"));

        var payload = (await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>())!;
        return payload.UserId;
    }

    private static async Task<LoginResponse> LoginAsync(HttpClient client, string email)
    {
        var loginResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(email, "StrongPass123!"));

        return (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private static async Task AddCompletedTasksAsync(
        AuthTestFactory factory,
        Guid userId,
        int count,
        DateTime baseUpdatedAtUtc)
    {
        for (var index = 0; index < count; index++)
        {
            await factory.AddTaskAsync(new TaskItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = $"Task {userId:N}-{index}",
                Description = "Suspicious-case fixture task",
                DueAtUtc = null,
                Priority = "medium",
                Category = "work",
                IsCompleted = true,
                CreatedAtUtc = baseUpdatedAtUtc.AddMinutes(-index),
                UpdatedAtUtc = baseUpdatedAtUtc.AddMinutes(-index)
            });
        }
    }
}
