namespace TaskTracker.Api.Features.Tasks.Validation;

public static class TaskPayloadValidator
{
    private static readonly HashSet<string> AllowedPriorities = new(StringComparer.Ordinal)
    {
        "low",
        "medium",
        "high"
    };

    private static readonly HashSet<string> AllowedDifficulties = new(StringComparer.Ordinal)
    {
        "easy",
        "medium",
        "hard"
    };

    private static readonly HashSet<string> AllowedEnergyLevels = new(StringComparer.Ordinal)
    {
        "low",
        "medium",
        "high"
    };

    public static (bool IsValid, Dictionary<string, string[]> Errors) Validate(
        string? title,
        string? description,
        DateTime? dueAtUtc,
        string? priority,
        string? category,
        string? difficulty,
        string? energyLevel,
        string? contextTag,
        int? effortPoints)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(title))
        {
            errors["title"] = ["The title field is required."];
        }
        else if (title.Trim().Length > 160)
        {
            errors["title"] = ["The title field must be 160 characters or fewer."];
        }

        var normalizedDescription = description?.Trim() ?? string.Empty;
        if (normalizedDescription.Length > 2000)
        {
            errors["description"] = ["The description field must be 2000 characters or fewer."];
        }

        if (dueAtUtc.HasValue && dueAtUtc.Value.Kind != DateTimeKind.Utc)
        {
            errors["dueAtUtc"] = ["The dueAtUtc field must be a UTC datetime value."];
        }

        if (string.IsNullOrWhiteSpace(priority))
        {
            errors["priority"] = ["The priority field is required."];
        }
        else if (!AllowedPriorities.Contains(priority.Trim().ToLowerInvariant()))
        {
            errors["priority"] = ["The priority field must be one of: low, medium, high."];
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            errors["category"] = ["The category field is required."];
        }
        else if (category.Trim().Length > 64)
        {
            errors["category"] = ["The category field must be 64 characters or fewer."];
        }

        if (!string.IsNullOrWhiteSpace(difficulty)
            && !AllowedDifficulties.Contains(difficulty.Trim().ToLowerInvariant()))
        {
            errors["difficulty"] = ["The difficulty field must be one of: easy, medium, hard."];
        }

        if (!string.IsNullOrWhiteSpace(energyLevel)
            && !AllowedEnergyLevels.Contains(energyLevel.Trim().ToLowerInvariant()))
        {
            errors["energyLevel"] = ["The energyLevel field must be one of: low, medium, high."];
        }

        if (!string.IsNullOrWhiteSpace(contextTag) && contextTag.Trim().Length > 64)
        {
            errors["contextTag"] = ["The contextTag field must be 64 characters or fewer."];
        }

        if (effortPoints.HasValue && (effortPoints.Value < 1 || effortPoints.Value > 100))
        {
            errors["effortPoints"] = ["The effortPoints field must be between 1 and 100."];
        }

        return (errors.Count == 0, errors);
    }
}
