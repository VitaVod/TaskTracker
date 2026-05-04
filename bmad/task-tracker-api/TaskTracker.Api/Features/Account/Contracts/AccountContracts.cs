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

public record AccountEmailChangeRequest(string NewEmail, string CurrentPassword);

public record AccountEmailChangeRequestResponse(string Message);

public record AccountEmailChangeConfirmRequest(string Token);

public record AccountEmailChangeConfirmResponse(string Message);
