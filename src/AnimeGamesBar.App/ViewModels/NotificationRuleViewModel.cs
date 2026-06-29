using AnimeGamesBar.App.Services.Notifications;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App.ViewModels;

public sealed class NotificationRuleViewModel : ObservableObject
{
    private const string DailyCategory = "\u65E5\u5E38";
    private const string WeeklyCategory = "\u5468\u5E38";
    private const string PeriodCategory = "\u5468\u671F";
    private const string CompletionCategory = "\u5B8C\u6210\u65F6\u95F4";
    private const string GreaterThanOrEqual = "\u2265";
    private const string LessThanOrEqual = "\u2264";
    private const string Monday = "\u5468\u4E00";

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
    private bool _requireThresholdForWeekly;

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
        _operator = NormalizeOperator(setting?.Operator ?? GreaterThanOrEqual);
        _threshold = Math.Max(0, setting?.Threshold ?? 0);
        _useScheduledTime = setting?.UseScheduledTime ?? false;
        _hour = ClampWhole(setting?.Hour ?? 9, 0, 23);
        _minute = ClampWhole(setting?.Minute ?? 0, 0, 59);
        _weekday = NormalizeWeekday(setting?.Weekday ?? Monday);
        _daysBefore = ClampWhole(setting?.DaysBefore ?? 1, 0, 30);
        _requireThresholdForPeriod = setting?.RequireThresholdForPeriod ?? false;
        _requireThresholdForWeekly = setting?.RequireThresholdForWeekly ?? true;
    }

    public static IReadOnlyList<string> CategoryOptions { get; } = new[]
    {
        DailyCategory,
        WeeklyCategory,
        PeriodCategory,
        CompletionCategory
    };

    public static IReadOnlyList<string> OperatorOptions { get; } = new[] { GreaterThanOrEqual, ">", LessThanOrEqual, "<" };

    public static IReadOnlyList<string> WeekdayOptions { get; } = new[]
    {
        Monday,
        "\u5468\u4E8C",
        "\u5468\u4E09",
        "\u5468\u56DB",
        "\u5468\u4E94",
        "\u5468\u516D",
        "\u5468\u65E5"
    };

    public IReadOnlyList<string> CategoryItems => CategoryOptions;

    public IReadOnlyList<string> OperatorItems => OperatorOptions;

    public IReadOnlyList<string> WeekdayItems => WeekdayOptions;

    public string Id { get; }

    public string GameTitle { get; }

    public string MetricTitle { get; }

    public string MetricId { get; }

    public Visibility DailyRuleVisibility => Category == DailyCategory ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WeeklyRuleVisibility => Category == WeeklyCategory ? Visibility.Visible : Visibility.Collapsed;

    public Visibility PeriodRuleVisibility => Category == PeriodCategory ? Visibility.Visible : Visibility.Collapsed;

    public Visibility CompletionRuleVisibility => Category == CompletionCategory ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WeeklyThresholdVisibility => RequireThresholdForWeekly ? Visibility.Visible : Visibility.Collapsed;

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public string Category
    {
        get => _category;
        set
        {
            if (SetProperty(ref _category, NormalizeCategory(value)))
            {
                OnPropertyChanged(nameof(DailyRuleVisibility));
                OnPropertyChanged(nameof(WeeklyRuleVisibility));
                OnPropertyChanged(nameof(PeriodRuleVisibility));
                OnPropertyChanged(nameof(CompletionRuleVisibility));
            }
        }
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

    public bool RequireThresholdForWeekly
    {
        get => _requireThresholdForWeekly;
        set
        {
            if (SetProperty(ref _requireThresholdForWeekly, value))
            {
                OnPropertyChanged(nameof(WeeklyThresholdVisibility));
            }
        }
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
            RequireThresholdForPeriod,
            RequireThresholdForWeekly);
    }

    private static double ClampWhole(double value, double minimum, double maximum)
    {
        return Math.Clamp(double.IsNaN(value) ? minimum : Math.Round(value), minimum, maximum);
    }

    private static string NormalizeCategory(string? value)
    {
        return CategoryOptions.Contains(value) ? value! : DailyCategory;
    }

    private static string NormalizeOperator(string? value)
    {
        return OperatorOptions.Contains(value) ? value! : GreaterThanOrEqual;
    }

    private static string NormalizeWeekday(string? value)
    {
        return WeekdayOptions.Contains(value) ? value! : Monday;
    }
}
