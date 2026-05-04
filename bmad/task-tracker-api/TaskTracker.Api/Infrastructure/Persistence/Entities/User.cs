namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string PasswordSalt { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = "UTC";

    public string Locale { get; set; } = "en-US";

    public string Role { get; set; } = "User";

    public LeaderboardParticipationMode LeaderboardParticipationMode { get; set; } = LeaderboardParticipationMode.Public;

    public bool IsSuspiciousFlagged { get; set; }

    public bool ReminderEmailEnabled { get; set; } = true;

    public NotificationReminderCadence ReminderCadence { get; set; } = NotificationReminderCadence.Daily;

    public bool AccountEmailEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ModifiedAtUtc { get; set; }
}