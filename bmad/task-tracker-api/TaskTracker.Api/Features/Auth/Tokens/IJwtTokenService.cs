namespace TaskTracker.Api.Features.Auth.Tokens;

public interface IJwtTokenService
{
    /// <summary>
    /// Issues an access JWT that includes the session identifier as a claim,
    /// enabling server-side revocation checks on protected endpoints.
    /// </summary>
    string CreateAccessToken(Guid userId, string email, string role, Guid sessionId);

    /// <summary>
    /// Issues a refresh JWT whose JTI equals <paramref name="sessionId"/> so
    /// the session can be looked up without storing the raw token string.
    /// </summary>
    string CreateRefreshToken(Guid userId, string email, string role, Guid sessionId);

    /// <summary>
    /// Validates the refresh token signature, lifetime, and type claims.
    /// Returns <c>true</c> and populates out params on success.
    /// </summary>
    bool TryValidateRefreshToken(
        string token,
        out Guid sessionId,
        out Guid userId,
        out string email);

    int AccessTokenLifetimeInSeconds { get; }

    int RefreshTokenLifetimeDays { get; }
}