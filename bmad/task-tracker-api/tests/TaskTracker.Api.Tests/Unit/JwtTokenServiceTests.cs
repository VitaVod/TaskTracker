using Microsoft.Extensions.Options;
using TaskTracker.Api.Features.Auth.Tokens;

namespace TaskTracker.Api.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="JwtTokenService"/> covering token creation,
/// validation, and the refresh-token lifecycle critical paths.
/// </summary>
public class JwtTokenServiceTests
{
    private readonly JwtTokenService _service;
    private readonly Guid _userId = Guid.NewGuid();
    private const string Email = "unit.test@example.com";
    private const string Role = "User";

    public JwtTokenServiceTests()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "task-tracker-api",
            Audience = "task-tracker-web",
            SigningKey = "unit-test-signing-key-must-be-at-least-32-chars!!",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });

        _service = new JwtTokenService(options);
    }

    // ── Refresh token validation ───────────────────────────────────────────────

    [Fact]
    public void TryValidateRefreshToken_ValidToken_ReturnsTrueAndExtractsSessionId()
    {
        var sessionId = Guid.NewGuid();
        var token = _service.CreateRefreshToken(_userId, Email, Role, sessionId);

        var result = _service.TryValidateRefreshToken(token, out var extractedSessionId, out var extractedUserId, out var extractedEmail);

        Assert.True(result);
        Assert.Equal(sessionId, extractedSessionId);
        Assert.Equal(_userId, extractedUserId);
        Assert.Equal(Email, extractedEmail);
    }

    [Fact]
    public void TryValidateRefreshToken_AccessTokenPresented_ReturnsFalse()
    {
        var sessionId = Guid.NewGuid();
        var accessToken = _service.CreateAccessToken(_userId, Email, Role, sessionId);

        var result = _service.TryValidateRefreshToken(accessToken, out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryValidateRefreshToken_MalformedToken_ReturnsFalse()
    {
        var result = _service.TryValidateRefreshToken("not.a.jwt", out _, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void TryValidateRefreshToken_TokenWithWrongSignature_ReturnsFalse()
    {
        var otherOptions = Options.Create(new JwtOptions
        {
            Issuer = "task-tracker-api",
            Audience = "task-tracker-web",
            SigningKey = "different-signing-key-that-is-also-at-least-32-chars!",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        });
        var otherService = new JwtTokenService(otherOptions);
        var sessionId = Guid.NewGuid();
        var tokenFromOtherKey = otherService.CreateRefreshToken(_userId, Email, Role, sessionId);

        var result = _service.TryValidateRefreshToken(tokenFromOtherKey, out _, out _, out _);

        Assert.False(result);
    }

    // ── Access token session_id claim ─────────────────────────────────────────

    [Fact]
    public void CreateAccessToken_IncludesSessionIdClaim()
    {
        var sessionId = Guid.NewGuid();
        var token = _service.CreateAccessToken(_userId, Email, Role, sessionId);

        // Decode payload without re-validating (the test just verifies the claim is present)
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var sessionIdClaim = jwt.Claims.FirstOrDefault(c => c.Type == "session_id")?.Value;
        Assert.Equal(sessionId.ToString(), sessionIdClaim);
    }

    [Fact]
    public void CreateRefreshToken_JtiEqualsSessionId()
    {
        var sessionId = Guid.NewGuid();
        var token = _service.CreateRefreshToken(_userId, Email, Role, sessionId);

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var jti = jwt.Claims.FirstOrDefault(c => c.Type == "jti")?.Value;
        Assert.Equal(sessionId.ToString(), jti);
    }

    [Fact]
    public void CreateAccessToken_IncludesRoleClaim()
    {
        var sessionId = Guid.NewGuid();
        var token = _service.CreateAccessToken(_userId, Email, Role, sessionId);

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var role = jwt.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;
        Assert.Equal(Role, role);
    }

    // ── Lifetime properties ───────────────────────────────────────────────────

    [Fact]
    public void AccessTokenLifetimeInSeconds_Returns900ForDefault15Minutes()
    {
        Assert.Equal(900, _service.AccessTokenLifetimeInSeconds);
    }

    [Fact]
    public void RefreshTokenLifetimeDays_Returns7ForDefault()
    {
        Assert.Equal(7, _service.RefreshTokenLifetimeDays);
    }
}
