using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TaskTracker.Api.Features.Auth.Contracts;
using TaskTracker.Api.Features.Auth.Email;
using TaskTracker.Api.Infrastructure.Authorization;
using TaskTracker.Api.Infrastructure.Persistence.Entities;

namespace TaskTracker.Api.Tests.Integration;

public class NotificationRemindersControllerTests : IClassFixture<AuthTestFactory>
{
    private readonly HttpClient _client;
    private readonly AuthTestFactory _factory;

    public NotificationRemindersControllerTests(AuthTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Run_WithEligibleAndDisabledUsers_SendsOnlyForEnabledUsers()
    {
        await _factory.ResetStateAsync();
        var emailService = _factory.GetRecoveryEmailService();
        emailService.ClearReminderAttempts();

        var enabledUser = await RegisterAndLoginWithUserAsync("reminders.enabled@example.com");
        var disabledUser = await RegisterAndLoginWithUserAsync("reminders.disabled@example.com");
        var admin = await RegisterAndLoginWithUserAsync("reminders.admin@example.com", AppRoles.Admin);

        await _factory.UpsertStreakSnapshotAsync(BuildNearMissSnapshot(enabledUser.UserId));
        await _factory.UpsertStreakSnapshotAsync(BuildNearMissSnapshot(disabledUser.UserId));

        await _factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = enabledUser.UserId,
            Title = "Pending one",
            Description = string.Empty,
            Priority = "medium",
            Category = "general",
            IsCompleted = false,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
        });

        await _factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = enabledUser.UserId,
            Title = "Already done",
            Description = string.Empty,
            Priority = "medium",
            Category = "general",
            IsCompleted = true,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-9),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-9)
        });

        await _factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = disabledUser.UserId,
            Title = "Disabled task",
            Description = string.Empty,
            Priority = "low",
            Category = "general",
            IsCompleted = false,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-8),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-8)
        });

        await PatchPreferencesAsync(disabledUser.Tokens.AccessToken, new { reminderEmailEnabled = false });

        var runResponse = await RunReminderJobAsAdminAsync(admin.Tokens.AccessToken);
        var payload = await runResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, runResponse.StatusCode);
        Assert.True(payload.GetProperty("sentCount").GetInt32() >= 1);

        var attempted = emailService.GetAttemptedReminderMessages();
        var enabledMessage = Assert.Single(attempted, message => message.UserId == enabledUser.UserId);
        Assert.DoesNotContain(attempted, message => message.UserId == disabledUser.UserId);
        Assert.All(enabledMessage.Tasks, task => Assert.NotEqual("Already done", task.Title));
    }

    [Fact]
    public async Task Run_RepeatedInSameDailyWindow_DeduplicatesByCadenceWindow()
    {
        await _factory.ResetStateAsync();
        var emailService = _factory.GetRecoveryEmailService();
        emailService.ClearReminderAttempts();

        var user = await RegisterAndLoginWithUserAsync("reminders.daily.dedupe@example.com");
        var admin = await RegisterAndLoginWithUserAsync("reminders.daily.admin@example.com", AppRoles.Admin);

        await _factory.UpsertStreakSnapshotAsync(BuildNearMissSnapshot(user.UserId));

        await _factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            Title = "Daily reminder task",
            Description = string.Empty,
            Priority = "high",
            Category = "focus",
            IsCompleted = false,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-20)
        });

        var first = await RunReminderJobAsAdminAsync(admin.Tokens.AccessToken);
        var second = await RunReminderJobAsAdminAsync(admin.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(emailService.GetAttemptedReminderMessages(), message => message.UserId == user.UserId);

        var successfulDispatches = await _factory.CountReminderDispatchesAsync(user.UserId, NotificationReminderDispatchStatus.Succeeded);
        Assert.Equal(1, successfulDispatches);
    }

    [Fact]
    public async Task Run_TransientReminderFailures_AreRetriedAndEventuallySucceed()
    {
        await _factory.ResetStateAsync();
        var emailService = _factory.GetRecoveryEmailService();
        emailService.ClearReminderAttempts();
        emailService.SetNextReminderResults(
            TransactionalEmailSendResult.TransientFailure,
            TransactionalEmailSendResult.TransientFailure,
            TransactionalEmailSendResult.Success);

        var user = await RegisterAndLoginWithUserAsync("reminders.retry@example.com");
        var admin = await RegisterAndLoginWithUserAsync("reminders.retry.admin@example.com", AppRoles.Admin);

        await _factory.UpsertStreakSnapshotAsync(BuildNearMissSnapshot(user.UserId));

        await _factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            Title = "Retry reminder task",
            Description = string.Empty,
            Priority = "high",
            Category = "general",
            IsCompleted = false,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-30),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-30)
        });

        var response = await RunReminderJobAsAdminAsync(admin.Tokens.AccessToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.GetProperty("sentCount").GetInt32() >= 1);
        Assert.Equal(3, emailService.GetAttemptedReminderMessages().Count(message => message.UserId == user.UserId));
    }

    [Fact]
    public async Task Run_PermanentFailure_LogsStructuredFailureAndTracksDispatch()
    {
        await _factory.ResetStateAsync();
        var emailService = _factory.GetRecoveryEmailService();
        emailService.ClearReminderAttempts();
        _factory.ClearCapturedLogs();
        emailService.SetNextReminderResults(TransactionalEmailSendResult.PermanentFailure);

        var user = await RegisterAndLoginWithUserAsync("reminders.permanent@example.com");
        var admin = await RegisterAndLoginWithUserAsync("reminders.permanent.admin@example.com", AppRoles.Admin);

        await _factory.UpsertStreakSnapshotAsync(BuildNearMissSnapshot(user.UserId));

        await _factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            Title = "Permanent failure reminder task",
            Description = string.Empty,
            Priority = "medium",
            Category = "general",
            IsCompleted = false,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-40),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-40)
        });

        var response = await RunReminderJobAsAdminAsync(admin.Tokens.AccessToken);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.GetProperty("failedCount").GetInt32() >= 1);

        var logs = _factory.GetCapturedLogs();
        Assert.Contains(logs, entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains("Reminder delivery failed permanently", StringComparison.Ordinal)
            && entry.Message.Contains("TraceId=", StringComparison.Ordinal));

        var failedDispatches = await _factory.CountReminderDispatchesAsync(user.UserId, NotificationReminderDispatchStatus.FailedPermanent);
        Assert.Equal(1, failedDispatches);
    }

    [Fact]
    public async Task Run_WhenUserIsNotNearMiss_DoesNotSendReminder()
    {
        await _factory.ResetStateAsync();
        var emailService = _factory.GetRecoveryEmailService();
        emailService.ClearReminderAttempts();

        var user = await RegisterAndLoginWithUserAsync("reminders.not-near-miss@example.com");
        var admin = await RegisterAndLoginWithUserAsync("reminders.not-near-miss.admin@example.com", AppRoles.Admin);

        await _factory.UpsertStreakSnapshotAsync(new UserStreakSnapshot
        {
            OwnerId = user.UserId,
            Outcome = TaskTracker.Api.Features.Tasks.Contracts.TaskStreakOutcome.Continue,
            CurrentStreakDays = 2,
            LongestStreakDays = 2,
            TimeZoneId = "UTC",
            EvaluationWindowStartUtc = DateTime.UtcNow.AddDays(-1),
            EvaluationWindowEndUtc = DateTime.UtcNow,
            LastEvaluatedEventId = Guid.NewGuid(),
            LastEvaluationTraceId = "trace-not-near-miss",
            LastEvaluatedAtUtc = DateTime.UtcNow
        });

        await _factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            Title = "No near miss task",
            Description = string.Empty,
            Priority = "medium",
            Category = "general",
            IsCompleted = false,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-20),
            UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-20)
        });

        var response = await RunReminderJobAsAdminAsync(admin.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(emailService.GetAttemptedReminderMessages(), message => message.UserId == user.UserId);
    }

    [Fact]
    public async Task Run_WithExistingLocalDayDispatch_SkipsDuplicateNearMissNudge()
    {
        await _factory.ResetStateAsync();
        var emailService = _factory.GetRecoveryEmailService();
        emailService.ClearReminderAttempts();

        var user = await RegisterAndLoginWithUserAsync("reminders.nearmiss.localday@example.com");
        var admin = await RegisterAndLoginWithUserAsync("reminders.nearmiss.localday.admin@example.com", AppRoles.Admin);

        const string timeZoneId = "Pacific Standard Time";
        await _factory.SetUserTimeZoneAsync(user.UserId, timeZoneId);

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var nowUtc = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var localDate = DateOnly.FromDateTime(localNow);
        var localWindowStartUtc = TimeZoneInfo.ConvertTimeToUtc(localDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), timeZone);

        await _factory.UpsertStreakSnapshotAsync(new UserStreakSnapshot
        {
            OwnerId = user.UserId,
            Outcome = TaskTracker.Api.Features.Tasks.Contracts.TaskStreakOutcome.Continue,
            CurrentStreakDays = 5,
            LongestStreakDays = 8,
            TimeZoneId = timeZoneId,
            EvaluationWindowStartUtc = localWindowStartUtc.AddDays(-1),
            EvaluationWindowEndUtc = localWindowStartUtc,
            LastEvaluatedEventId = Guid.NewGuid(),
            LastEvaluationTraceId = "trace-localday-dedupe",
            LastEvaluatedAtUtc = localWindowStartUtc.AddHours(-1)
        });

        await _factory.AddTaskAsync(new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            Title = "Near miss local day",
            Description = string.Empty,
            Priority = "high",
            Category = "focus",
            IsCompleted = false,
            CreatedAtUtc = nowUtc.AddMinutes(-30),
            UpdatedAtUtc = nowUtc.AddMinutes(-30)
        });

        await _factory.AddReminderDispatchAsync(new NotificationReminderDispatch
        {
            Id = Guid.NewGuid(),
            UserId = user.UserId,
            Cadence = NotificationReminderCadence.Daily,
            WindowStartUtc = localWindowStartUtc,
            WindowEndUtc = localWindowStartUtc.AddDays(1),
            Status = NotificationReminderDispatchStatus.Succeeded,
            AttemptCount = 1,
            TaskCount = 1,
            CreatedAtUtc = nowUtc.AddMinutes(-5),
            SentAtUtc = nowUtc.AddMinutes(-5),
            TraceId = "trace-existing-localday"
        });

        var response = await RunReminderJobAsAdminAsync(admin.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(emailService.GetAttemptedReminderMessages(), message => message.UserId == user.UserId);
    }

    [Fact]
    public async Task Run_WithoutAdminRole_ReturnsForbidden()
    {
        await _factory.ResetStateAsync();
        var nonAdmin = await RegisterAndLoginWithUserAsync("reminders.forbidden@example.com");

        var response = await RunReminderJobAsAdminAsync(nonAdmin.Tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private async Task PatchPreferencesAsync(string accessToken, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/notifications/preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpResponseMessage> RunReminderJobAsAdminAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/internal/notifications/reminders/run");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await _client.SendAsync(request);
    }

    private static UserStreakSnapshot BuildNearMissSnapshot(Guid userId)
    {
        var nowUtc = DateTime.UtcNow;
        return new UserStreakSnapshot
        {
            OwnerId = userId,
            Outcome = TaskTracker.Api.Features.Tasks.Contracts.TaskStreakOutcome.Continue,
            CurrentStreakDays = 4,
            LongestStreakDays = 6,
            TimeZoneId = "UTC",
            EvaluationWindowStartUtc = nowUtc.AddDays(-1),
            EvaluationWindowEndUtc = nowUtc,
            RecoveryTokenBalance = 0,
            RecoveryTokenWeekKey = string.Empty,
            LastEvaluatedEventId = Guid.NewGuid(),
            LastEvaluationTraceId = "trace-near-miss",
            LastEvaluatedAtUtc = nowUtc.AddDays(-1)
        };
    }
}
