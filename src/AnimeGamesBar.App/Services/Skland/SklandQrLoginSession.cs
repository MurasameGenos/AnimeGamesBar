namespace AnimeGamesBar.App.Services.Skland;

public sealed record SklandQrLoginSession(string SessionId, Uri QrCodeUri, DateTimeOffset ExpiresAt);
