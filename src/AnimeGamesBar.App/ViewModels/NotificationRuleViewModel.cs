using AnimeGamesBar.App.Services.Notifications;

namespace AnimeGamesBar.App.ViewModels;

public sealed class NotificationRuleViewModel : ObservableObject
{
    private bool _enabled;
    private string _category;
    private string _operator;
    private double _threshold;
    private bool _useScheduledTime;
    private double _hour;
    private double _minute;
    private string _weekday;
    private double _daysBefore;
    private bool _requireThresholdForPeriod;

    public NotificationRuleViewModel(
        string id,
        string gameTitle,
        string metricTitle,
        string metricId,
        string defaultCategory,
        NotificationRuleSetting? setting)
    {
        Id = id;
        GameTitle = gameTitle;
        MetricTitle = metricTitle;
        MetricId = metricId;
        _enabled = setting?.Enabled ?? false;
        _category = NormalizeCategory(setting?.Category ?? defaultCategory);
        _operator = NormalizeOperator(setting?.Operator ?? "≥");
        _threshold = Math.Max(0, setting?.Threshold ?? 0);
        _useScheduledTime = setting?.UseScheduledTime ?? false;
        _hour = ClampWhole(setting?.Hour ?? 9, 0, 23);
        _minute = ClampWhole(setting?.Minute ?? 0, 0, 59);
        _weekday = NormalizeWeekday(setting?.Weekday ?? "周一");
        _daysBefore = ClampWhole(setting?.DaysBefore ?? 1, 0, 30);
        _requireThresholdForPeriod = setting?.RequireThresholdForPeriod ?? false;
    }

    public static IReadOnlyList<string> CategoryOptions { get; } = new[] { "日常", "周常", "周期" };

    public static IReadOnlyList<string> OperatorOptions { get; } = new[] { "≥", ">", "≤", "<" };

    public static IReadOnlyList<string> WeekdayOptions { get; } = new[] { "周一", "周二", "周三", "周四", "周五", "周六", "周日" };

    public IReadOnlyList<string> CategoryItems => CategoryOptions;

    public IReadOnlyList<string> OperatorItems => OperatorOptions;

    public IReadOnlyList<string> WeekdayItems => WeekdayOptions;

    public string Id { get; }

    public string GameTitle { get; }

    public string MetricTitle { get; }

    public string MetricId { get; }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, NormalizeCategory(value));
    }

    public string Operator
    {
        get => _operator;
        set => SetProperty(ref _operator, NormalizeOperator(value));
    }

    public double Threshold
    {
        get => _threshold;
        set => SetProperty(ref _threshold, Math.Max(0, Math.Round(value)));
    }

    public bool UseScheduledTime
    {
        get => _useScheduledTime;
        set => SetProperty(ref _useScheduledTime, value);
    }

    public double Hour
    {
        get => _hour;
        set => SetProperty(ref _hour, ClampWhole(value, 0, 23));
    }

    public double Minute
    {
        get => _minute;
        set => SetProperty(ref _minute, ClampWhole(value, 0, 59));
    }

    public string Weekday
    {
        get => _weekday;
        set => SetProperty(ref _weekday, NormalizeWeekday(value));
    }

    public double DaysBefore
    {
        get => _daysBefore;
        set => SetProperty(ref _daysBefore, ClampWhole(value, 0, 30));
    }

    public bool RequireThresholdForPeriod
    {
        get => _requireThresholdForPeriod;
        set => SetProperty(ref _requireThresholdForPeriod, value);
    }

    public NotificationRuleSetting ToSetting()
    {
        return new NotificationRuleSetting(
            Id,
            Enabled,
            MetricId,
            Category,
            Operator,
            Threshold,
            UseScheduledTime,
            Hour,
            Minute,
            Weekday,
            DaysBefore,
            RequireThresholdForPeriod);
    }

    private static double ClampWhole(double value, double minimum, double maximum)
    {
        return Math.Clamp(double.IsNaN(value) ? minimum : Math.Round(value), minimum, maximum);
    }

    private static string NormalizeCategory(string? value)
    {
        return CategoryOptions.Contains(value) ? value! : "日常";
    }

    private static string NormalizeOperator(string? value)
    {
        return OperatorOptions.Contains(value) ? value! : "≥";
    }

    private static string NormalizeWeekday(string? value)
    {
        return WeekdayOptions.Contains(value) ? value! : "周一";
    }
}
