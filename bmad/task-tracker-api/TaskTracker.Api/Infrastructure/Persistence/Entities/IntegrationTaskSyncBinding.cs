namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class IntegrationTaskSyncBinding
{
    public Guid Id { get; set; }

    public Guid OwnerUserId { get; set; }

    public string IntegrationId { get; set; } = string.Empty;

    public string ExternalTaskId { get; set; } = string.Empty;

    public Guid TaskId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
