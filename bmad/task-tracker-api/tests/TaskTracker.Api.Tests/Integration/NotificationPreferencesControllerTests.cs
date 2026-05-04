using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskTracker.Api.Features.Auth.Contracts;

namespace TaskTracker.Api.Tests.Integration;

public class NotificationPreferencesControllerTests : IClassFixture<AuthTestFactory>
{
    private readonly HttpClient _client;

    public NotificationPreferencesControllerTests(AuthTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetPreferences_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/notifications/preferences");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/authentication-failed", payload.GetProperty("type").GetString());
        Assert.Equal("auth.session.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task GetPreferences_ForNewUser_ReturnsDeterministicDefaults()
    {
        var tokens = await RegisterAndLoginAsync("notifications.defaults@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(payload.GetProperty("reminderEmailEnabled").GetBoolean());
        Assert.Equal("daily", payload.GetProperty("reminderCadence").GetString());
        Assert.True(payload.GetProperty("accountEmailEnabled").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("updatedAtUtc").GetString()));
    }

    [Fact]
    public async Task PatchPreferences_WithValidPayload_PersistsAcrossRequests()
    {
        var tokens = await RegisterAndLoginAsync("notifications.persist@example.com");

        await PatchAsync(tokens.AccessToken, new
        {
            reminderEmailEnabled = false,
            reminderCadence = "weekly",
            accountEmailEnabled = false
        });

        using var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/preferences");
        getRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var getResponse = await _client.SendAsync(getRequest);
        var getPayload = await getResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.False(getPayload.GetProperty("reminderEmailEnabled").GetBoolean());
        Assert.Equal("weekly", getPayload.GetProperty("reminderCadence").GetString());
        Assert.False(getPayload.GetProperty("accountEmailEnabled").GetBoolean());
    }

    [Fact]
    public async Task PatchPreferences_WithIdenticalPayload_IsIdempotentAndStable()
    {
        var tokens = await RegisterAndLoginAsync("notifications.idempotent@example.com");

        await PatchAsync(tokens.AccessToken, new
        {
            reminderEmailEnabled = false,
            reminderCadence = "weekly",
            accountEmailEnabled = true
        });

        var firstRead = await GetPreferencesAsync(tokens.AccessToken);
        var firstUpdatedAt = firstRead.GetProperty("updatedAtUtc").GetString();

        await PatchAsync(tokens.AccessToken, new
        {
            reminderEmailEnabled = false,
            reminderCadence = "weekly",
            accountEmailEnabled = true
        });

        var secondRead = await GetPreferencesAsync(tokens.AccessToken);
        var secondUpdatedAt = secondRead.GetProperty("updatedAtUtc").GetString();

        Assert.Equal(firstUpdatedAt, secondUpdatedAt);
    }

    [Fact]
    public async Task PatchPreferences_WithUnknownField_ReturnsValidationProblemDetails()
    {
        var tokens = await RegisterAndLoginAsync("notifications.invalidfield@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/notifications/preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Content = JsonContent.Create(new { userId = Guid.NewGuid() });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("notifications.preferences.validation_failed", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        Assert.True(payload.GetProperty("errors").TryGetProperty("userId", out _));
    }

    [Fact]
    public async Task PatchPreferences_InvalidCadence_ReturnsValidationProblemDetails()
    {
        var tokens = await RegisterAndLoginAsync("notifications.cadence.invalid@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/notifications/preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Content = JsonContent.Create(new { reminderCadence = "monthly" });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("notifications.preferences.validation_failed", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        Assert.True(payload.GetProperty("errors").TryGetProperty("reminderCadence", out _));
    }

    [Fact]
    public async Task PatchPreferences_DoesNotAffectOtherUsers()
    {
        var firstUser = await RegisterAndLoginAsync("notifications.owner.one@example.com");
        var secondUser = await RegisterAndLoginAsync("notifications.owner.two@example.com");

        await PatchAsync(firstUser.AccessToken, new
        {
            reminderEmailEnabled = false,
            reminderCadence = "weekly",
            accountEmailEnabled = false
        });

        var secondRead = await GetPreferencesAsync(secondUser.AccessToken);

        Assert.True(secondRead.GetProperty("reminderEmailEnabled").GetBoolean());
        Assert.Equal("daily", secondRead.GetProperty("reminderCadence").GetString());
        Assert.True(secondRead.GetProperty("accountEmailEnabled").GetBoolean());
    }

    private async Task<LoginResponse> RegisterAndLoginAsync(string email)
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass123!"));
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "StrongPass123!"));
        return (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private async Task PatchAsync(string accessToken, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/notifications/preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<JsonElement> GetPreferencesAsync(string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notifications/preferences");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<JsonElement>())!;
    }
}
