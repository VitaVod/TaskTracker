namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

/// <summary>
/// Server-side session record that backs a single refresh-token issuance.
/// The <see cref="Id"/> is used directly as the JTI of the refresh JWT so sessions
/// can be looked up by token identifier without storing the raw token string.
/// </summary>
public class RefreshSession
{
    /// <summary>Used as the JTI claim in the issued refresh JWT.</summary>
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Denormalised email kept for audit queries without a join.</summary>
    public string UserEmail { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Null while the session is active.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>
    /// Reason the session was revoked: "logout", "rotated", or "replay-detected".
    /// Null while active.
    /// </summary>
    public string? RevokedReason { get; set; }

    /// <summary>
    /// For rotated sessions, the Id of the session that replaced this one.
    /// Enables replay detection: if a caller presents a rotated token, we can
    /// walk the chain and revoke the currently active descendant.
    /// </summary>
    public Guid? ReplacedBySessionId { get; set; }

    /// <summary>
    /// Optimistic concurrency token used to guarantee single-use refresh rotation
    /// under concurrent requests.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAtUtc { get; set; }
}
