namespace AnimeGamesBar.App.Services.Notifications;

public sealed record NotificationRuleSetting(
    string Id,
    bool Enabled,
    string MetricId,
    string Category,
    string Operator,
    double Threshold,
    bool UseScheduledTime,
    double Hour,
    double Minute,
    string Weekday,
    double DaysBefore,
    bool RequireThresholdForPeriod);
