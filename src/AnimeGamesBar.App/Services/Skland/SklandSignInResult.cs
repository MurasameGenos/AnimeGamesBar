namespace AnimeGamesBar.App.Services.Skland;

public enum SklandSignInState
{
    Success,
    AlreadySigned,
    Failed
}

public sealed record SklandSignInResult(
    string GameName,
    string RoleName,
    SklandSignInState State,
    string Message)
{
    public bool IsFailure => State == SklandSignInState.Failed;
}
