namespace AnimeGamesBar.App.Services.Skland;

public sealed record SklandWebCredentialSnapshot(
    string Cred,
    string Token,
    string Cookie,
    string UserId,
    string DeviceId)
{
    public bool HasAnySecret =>
        !string.IsNullOrWhiteSpace(Cred) ||
        !string.IsNullOrWhiteSpace(Token) ||
        !string.IsNullOrWhiteSpace(Cookie);
}
