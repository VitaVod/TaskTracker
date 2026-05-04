namespace TaskTracker.Api.Features.Progress.Configuration;

public class ProgressionLevelOptions
{
    public const string SectionName = "ProgressionLevels";

    public int BaseXpPerLevel { get; set; } = 100;

    public int GrowthXpPerLevel { get; set; } = 25;

    public int StartingLevel { get; set; } = 1;

    public int[] BandMilestoneLevels { get; set; } = [3, 5, 10, 20, 30, 50];
}
