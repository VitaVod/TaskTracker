using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using TaskTracker.Api.Features.Auth.Contracts;

namespace TaskTracker.Api.Tests.Integration;

public class AccountControllerTests : IClassFixture<AuthTestFactory>
{
    private readonly HttpClient _client;

    public AccountControllerTests(AuthTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetMe_WithoutAuthentication_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/account/me");
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("https://api.tasktracker.local/problems/authentication-failed", payload.GetProperty("type").GetString());
        Assert.Equal("Authentication Failed", payload.GetProperty("title").GetString());
        Assert.Equal(401, payload.GetProperty("status").GetInt32());
        Assert.Equal("auth.session.invalid", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task GetMe_WithAuthenticatedUser_ReturnsCurrentAccount()
    {
        var tokens = await RegisterAndLoginAsync("account.me@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("account.me@example.com", payload.GetProperty("email").GetString());
        Assert.Equal("UTC", payload.GetProperty("timeZoneId").GetString());
        Assert.Equal("en-US", payload.GetProperty("locale").GetString());
    }

    [Fact]
    public async Task PatchProfile_WithUnknownField_ReturnsValidationProblemDetails()
    {
        var tokens = await RegisterAndLoginAsync("account.unknown@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/account/profile");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Content = JsonContent.Create(new { email = "attempt@malicious.example" });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("account.profile.validation_failed", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        Assert.True(payload.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task PatchSettings_WithInvalidTimeZone_ReturnsValidationProblemDetails()
    {
        var tokens = await RegisterAndLoginAsync("account.tz@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/account/settings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        request.Content = JsonContent.Create(new { timeZoneId = "Invalid/Zone" });

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("account.settings.validation_failed", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
        Assert.True(payload.GetProperty("errors").TryGetProperty("timeZoneId", out _));
    }

    [Fact]
    public async Task PatchSettings_WithIanaTimeZone_PersistsSuccessfully()
    {
        var tokens = await RegisterAndLoginAsync("account.iana@example.com");

        await PatchAsync(tokens.AccessToken, "/api/v1/account/settings", new { timeZoneId = "Europe/Kyiv", locale = "uk-UA" });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var meResponse = await _client.SendAsync(request);
        var mePayload = await meResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal("Europe/Kyiv", mePayload.GetProperty("timeZoneId").GetString());
        Assert.Equal("uk-UA", mePayload.GetProperty("locale").GetString());
    }

    [Fact]
    public async Task PatchProfileAndSettings_WithValidPayload_PersistsChanges()
    {
        var tokens = await RegisterAndLoginAsync("account.update@example.com");

        await PatchAsync(tokens.AccessToken, "/api/v1/account/profile", new { displayName = "Alex Runner" });
        await PatchAsync(tokens.AccessToken, "/api/v1/account/settings", new { timeZoneId = "UTC", locale = "uk-UA" });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/account/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var meResponse = await _client.SendAsync(request);
        var mePayload = await meResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        Assert.Equal("Alex Runner", mePayload.GetProperty("displayName").GetString());
        Assert.Equal("UTC", mePayload.GetProperty("timeZoneId").GetString());
        Assert.Equal("uk-UA", mePayload.GetProperty("locale").GetString());
    }

    [Fact]
    public async Task GetUserById_AsNonOwnerStandardUser_ReturnsForbiddenOwnershipProblem()
    {
        var firstUser = await RegisterAndLoginWithUserAsync("owner.one@example.com");
        var secondUser = await RegisterAndLoginWithUserAsync("owner.two@example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/account/users/{secondUser.UserId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstUser.Tokens.AccessToken);

        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("https://api.tasktracker.local/problems/forbidden", payload.GetProperty("type").GetString());
        Assert.Equal("Forbidden", payload.GetProperty("title").GetString());
        Assert.Equal(403, payload.GetProperty("status").GetInt32());
        Assert.Equal("authz.ownership.denied", payload.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("traceId").GetString()));
    }

    private async Task<LoginResponse> RegisterAndLoginAsync(string email)
    {
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass123!"));
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "StrongPass123!"));
        return (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;
    }

    private async Task<(Guid UserId, LoginResponse Tokens)> RegisterAndLoginWithUserAsync(string email)
    {
        var registerResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest(email, "StrongPass123!"));
        var registerPayload = (await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>())!;

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "StrongPass123!"));
        var loginPayload = (await loginResponse.Content.ReadFromJsonAsync<LoginResponse>())!;

        return (registerPayload.UserId, loginPayload);
    }

    private async Task PatchAsync(string accessToken, string path, object payload)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(payload);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
