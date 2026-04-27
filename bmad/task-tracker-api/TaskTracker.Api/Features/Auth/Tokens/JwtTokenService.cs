using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace TaskTracker.Api.Features.Auth.Tokens;

public class JwtTokenService(IOptions<JwtOptions> jwtOptions) : IJwtTokenService
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public int AccessTokenLifetimeInSeconds => _jwtOptions.AccessTokenMinutes * 60;

    public int RefreshTokenLifetimeDays => _jwtOptions.RefreshTokenDays;

    public string CreateAccessToken(Guid userId, string email, string role, Guid sessionId)
    {
        var extraClaims = new[]
        {
            new Claim("session_id", sessionId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        return BuildToken(
            userId,
            email,
            DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes),
            "access",
            jti: Guid.NewGuid().ToString(),
            extraClaims);
    }

    public string CreateRefreshToken(Guid userId, string email, string role, Guid sessionId)
    {
        // JTI = sessionId so the session record can be located by JTI alone.
        return BuildToken(
            userId,
            email,
            DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            "refresh",
            jti: sessionId.ToString(),
            extraClaims: [new Claim(ClaimTypes.Role, role)]);
    }

    public bool TryValidateRefreshToken(
        string token,
        out Guid sessionId,
        out Guid userId,
        out string email)
    {
        sessionId = default;
        userId = default;
        email = string.Empty;

        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
                ClockSkew = TimeSpan.Zero
            };

            var principal = handler.ValidateToken(token, validationParams, out _);

            var tokenType = principal.FindFirst("token_type")?.Value;
            if (!string.Equals(tokenType, "refresh", StringComparison.Ordinal))
                return false;

            var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
            var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var emailClaim = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

            if (!Guid.TryParse(jti, out sessionId)
                || !Guid.TryParse(sub, out userId)
                || emailClaim is null)
            {
                return false;
            }

            email = emailClaim;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private string BuildToken(
        Guid userId,
        string email,
        DateTime expiresAtUtc,
        string tokenType,
        string jti,
        IEnumerable<Claim> extraClaims)
    {
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, jti),
            new("token_type", tokenType)
        };

        claims.AddRange(extraClaims);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}