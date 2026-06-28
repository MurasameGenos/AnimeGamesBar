namespace AnimeGamesBar.App.Services.Skland;

public sealed record SklandCredential(
    string Cred,
    string Token,
    string Cookie,
    string UserId,
    string DeviceId,
    DateTimeOffset UpdatedAt)
{
    public bool HasAnySecret =>
        !string.IsNullOrWhiteSpace(Cred) ||
        !string.IsNullOrWhiteSpace(Token) ||
        !string.IsNullOrWhiteSpace(Cookie);

    public static SklandCredential Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        string.Empty,
        Guid.NewGuid().ToString("N"),
        DateTimeOffset.MinValue);
}
