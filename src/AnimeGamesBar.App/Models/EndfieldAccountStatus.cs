namespace AnimeGamesBar.App.Models;

public sealed record EndfieldAccountStatus(
    string PlayerName,
    string ServerName,
    ResourceMeter Sanity,
    ProgressStatus DailyActivity,
    ProgressStatus WeeklyTasks,
    ProgressStatus PassLevel,
    DateTimeOffset UpdatedAt)
{
    public static EndfieldAccountStatus Empty { get; } = new(
        "未登录",
        string.Empty,
        ResourceMeter.Empty,
        ProgressStatus.Empty,
        ProgressStatus.Empty,
        ProgressStatus.Empty,
        DateTimeOffset.MinValue);
}
