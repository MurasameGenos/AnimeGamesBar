namespace AnimeGamesBar.App.Models;

public sealed record WutheringWavesResourceStatus(
    string Name,
    int Current,
    int Maximum,
    DateTimeOffset? RefreshAt = null,
    DateTimeOffset? ExpireAt = null,
    string Value = "");
