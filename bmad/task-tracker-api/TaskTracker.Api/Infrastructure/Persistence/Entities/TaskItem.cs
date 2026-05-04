namespace TaskTracker.Api.Infrastructure.Persistence.Entities;

public class TaskItem
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime? DueAtUtc { get; set; }

    public string Priority { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public TaskDifficulty Difficulty { get; set; } = TaskDifficulty.Easy;

    public TaskEnergyLevel EnergyLevel { get; set; } = TaskEnergyLevel.Medium;

    public string? ContextTag { get; set; }

    public int? EffortPoints { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}