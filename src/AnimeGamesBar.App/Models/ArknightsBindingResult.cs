namespace AnimeGamesBar.App.Models;

public sealed record ArknightsBindingResult(
    string ResolvedUserId,
    IReadOnlyList<ArknightsPlayerBinding> Bindings);
