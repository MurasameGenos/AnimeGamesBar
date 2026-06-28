namespace AnimeGamesBar.App.Models;

public sealed record ProgressStatus(
    int Current,
    int Maximum,
    DateTimeOffset? CompleteAt = null)
{
    public static ProgressStatus Empty { get; } = new(0, 0, null);
}
