namespace AnimeGamesBar.App.Models;

public sealed record BuildingStatus(
    ProgressStatus Orders,
    ProgressStatus Manufacture,
    int TiredOperators)
{
    public static BuildingStatus Empty { get; } = new(ProgressStatus.Empty, ProgressStatus.Empty, 0);
}
