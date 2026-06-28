namespace AnimeGamesBar.App.Models;

public sealed record YihuanAccountStatus(
    string PlayerName,
    string ServerName,
    ResourceMeter NaturePixels,
    ResourceMeter CityVitality,
    ProgressStatus DailyActivity,
    ProgressStatus WeeklyBoss,
    bool HasSignedIn,
    DateTimeOffset UpdatedAt);
