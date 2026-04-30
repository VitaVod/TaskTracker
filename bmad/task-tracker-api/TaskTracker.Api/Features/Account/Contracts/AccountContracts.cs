namespace TaskTracker.Api.Features.Account.Contracts;

public record AccountMeResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string TimeZoneId,
    string Locale,
    string LeaderboardParticipationMode,
    DateTime UpdatedAtUtc);

public record AccountUpdateResponse(string Message);
