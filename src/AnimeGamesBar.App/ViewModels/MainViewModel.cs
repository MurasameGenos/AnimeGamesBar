using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Kuro;
using AnimeGamesBar.App.Services.Notifications;
using AnimeGamesBar.App.Services.Arknights;
using AnimeGamesBar.App.Services.Settings;
using AnimeGamesBar.App.Services.Skland;
using AnimeGamesBar.App.Services.Startup;
using AnimeGamesBar.App.Services.Tajiduo;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AnimeGamesBar.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const string ArknightsAppCode = "arknights";
    private const string EndfieldAppCode = "endfield";

    private readonly ICredentialStore _credentialStore;
    private readonly IArknightsMonitor _monitor;
    private readonly ISklandLoginService _loginService;
    private readonly ISklandSignInService _signInService;
    private readonly IKuroMonitor _kuroMonitor;
    private readonly IKuroSignInService _kuroSignInService;
    private readonly IKuroLoginService _kuroLoginService;
    private readonly ITajiduoMonitor _tajiduoMonitor;
    private readonly ITajiduoSignInService _tajiduoSignInService;
    private readonly ITajiduoLoginService _tajiduoLoginService;
    private readonly ISettingsStore _settingsStore;
    private readonly IAppNotificationService _notificationService;
    private readonly IStartupService _startupService;
    private readonly Dictionary<string, DateTimeOffset> _sentNotificationTimes = new(StringComparer.Ordinal);
    private readonly List<ArknightsPlayerBinding> _arknightsBindings = new();
    private readonly List<ArknightsPlayerBinding> _endfieldBindings = new();
    private readonly List<ArknightsPlayerBinding> _wutheringWavesBindings = new();
    private readonly List<ArknightsPlayerBinding> _yihuanBindings = new();

    private SklandCredential _arknightsCredential = SklandCredential.Empty;
    private SklandCredential _endfieldCredential = SklandCredential.Empty;
    private SklandCredential _wutheringWavesCredential = SklandCredential.Empty;
    private SklandCredential _yihuanCredential = SklandCredential.Empty;
    private string _cred = string.Empty;
    private string _token = string.Empty;
    private string _cookie = string.Empty;
    private string _userId = string.Empty;
    private string _deviceId = Guid.NewGuid().ToString("N");
    private string _doctorName = "\u672A\u767B\u5F55";
    private string _statusMessage = string.Empty;
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;
    private ArknightsPlayerBinding? _selectedArknightsBinding;
    private ArknightsPlayerBinding? _selectedEndfieldBinding;
    private ArknightsPlayerBinding? _selectedWutheringWavesBinding;
    private ArknightsPlayerBinding? _selectedYihuanBinding;
    private ArknightsAccountStatus? _arknightsSnapshot;
    private EndfieldAccountStatus? _endfieldSnapshot;
    private WutheringWavesAccountStatus? _wutheringWavesSnapshot;
    private YihuanAccountStatus? _yihuanSnapshot;
    private GameDashboardKind _selectedGame = GameDashboardKind.Arknights;
    private bool _arknightsAutoRefreshEnabled;
    private bool _endfieldAutoRefreshEnabled;
    private bool _wutheringWavesAutoRefreshEnabled;
    private bool _yihuanAutoRefreshEnabled;
    private double _arknightsAutoRefreshIntervalMinutes = 5;
    private double _endfieldAutoRefreshIntervalMinutes = 5;
    private double _wutheringWavesAutoRefreshIntervalMinutes = 5;
    private double _yihuanAutoRefreshIntervalMinutes = 5;
    private bool _isSettingsPageOpen;
    private bool _isNotificationRulesPageOpen;
    private bool _isLaunchPathsPageOpen;
    private bool _useDarkTheme = true;
    private bool _autoSignEnabled = true;
    private bool _dailyAutoSignEnabled = true;
    private bool _manualSignInAllGames;
    private bool _notificationsEnabled = true;
    private bool _serverChanEnabled;
    private string _serverChanSendKey = string.Empty;
    private bool _startWithWindows;
    private bool _credentialsLoaded;
    private DateOnly? _lastDailyAutoSignDate;
    private bool _settingsLoaded;
    private bool _isApplyingSettings;
    private bool _notificationRulesDirty;
    private bool _notificationCooldownEnabled;
    private bool _savedNotificationCooldownEnabled;
    private double _notificationCooldownMinutes = 60;
    private double _savedNotificationCooldownMinutes = 60;
    private string _arknightsGamePath = string.Empty;
    private string _arknightsScriptPath = string.Empty;
    private string _endfieldGamePath = string.Empty;
    private string _endfieldScriptPath = string.Empty;
    private string _wutheringWavesGamePath = string.Empty;
    private string _wutheringWavesScriptPath = string.Empty;
    private string _yihuanGamePath = string.Empty;
    private string _yihuanScriptPath = string.Empty;
    private IReadOnlyList<NotificationRuleSetting> _savedNotificationRuleSettings = Array.Empty<NotificationRuleSetting>();

    public MainViewModel(
        ICredentialStore credentialStore,
        IArknightsMonitor monitor,
        ISklandLoginService loginService,
        ISklandSignInService signInService,
        IKuroMonitor kuroMonitor,
        IKuroSignInService kuroSignInService,
        IKuroLoginService kuroLoginService,
        ITajiduoMonitor tajiduoMonitor,
        ITajiduoSignInService tajiduoSignInService,
        ITajiduoLoginService tajiduoLoginService,
        ISettingsStore settingsStore,
        IAppNotificationService notificationService,
        IStartupService startupService)
    {
        _credentialStore = credentialStore;
        _monitor = monitor;
        _loginService = loginService;
        _signInService = signInService;
        _kuroMonitor = kuroMonitor;
        _kuroSignInService = kuroSignInService;
        _kuroLoginService = kuroLoginService;
        _tajiduoMonitor = tajiduoMonitor;
        _tajiduoSignInService = tajiduoSignInService;
        _tajiduoLoginService = tajiduoLoginService;
        _settingsStore = settingsStore;
        _notificationService = notificationService;
        _startupService = startupService;

        RefreshCommand = new AsyncCommand(RefreshAsync);
        RefreshAllCommand = new AsyncCommand(RefreshAllAsync);
        RefreshArknightsCommand = new AsyncCommand(cancellationToken => RefreshAutoGameAsync(GameDashboardKind.Arknights, cancellationToken));
        RefreshEndfieldCommand = new AsyncCommand(cancellationToken => RefreshAutoGameAsync(GameDashboardKind.Endfield, cancellationToken));
        RefreshWutheringWavesCommand = new AsyncCommand(cancellationToken => RefreshAutoGameAsync(GameDashboardKind.WutheringWaves, cancellationToken));
        RefreshYihuanCommand = new AsyncCommand(cancellationToken => RefreshAutoGameAsync(GameDashboardKind.Yihuan, cancellationToken));
        SignInCommand = new AsyncCommand(SignInManualAsync);
        ScheduledSignInCommand = new AsyncCommand(cancellationToken => SignInAllAsync(cancellationToken, showNotification: NotificationsEnabled));
        EvaluateNotificationRulesCommand = new AsyncCommand(EvaluateNotificationRulesAsync);
        SaveCredentialCommand = new AsyncCommand(SaveCredentialAsync);
        ClearCredentialCommand = new AsyncCommand(ClearCredentialAsync);
        StartLoginCommand = new AsyncCommand(StartLoginAsync);
        LaunchGameCommand = new AsyncCommand(cancellationToken => LaunchConfiguredProgramAsync(launchScript: false, cancellationToken));
        LaunchScriptCommand = new AsyncCommand(cancellationToken => LaunchConfiguredProgramAsync(launchScript: true, cancellationToken));
        BrowseGamePathCommand = new AsyncCommand(cancellationToken => BrowseLaunchPathAsync(launchScript: false, cancellationToken));
        BrowseScriptPathCommand = new AsyncCommand(cancellationToken => BrowseLaunchPathAsync(launchScript: true, cancellationToken));
        OpenSettingsCommand = new AsyncCommand(_ =>
        {
            IsSettingsPageOpen = true;
            IsNotificationRulesPageOpen = false;
            IsLaunchPathsPageOpen = false;
            return Task.CompletedTask;
        });
        CloseSettingsCommand = new AsyncCommand(_ =>
        {
            IsSettingsPageOpen = false;
            IsNotificationRulesPageOpen = false;
            IsLaunchPathsPageOpen = false;
            return Task.CompletedTask;
        });
        OpenNotificationRulesSettingsCommand = new AsyncCommand(_ =>
        {
            IsLaunchPathsPageOpen = false;
            IsNotificationRulesPageOpen = true;
            return Task.CompletedTask;
        });
        CloseNotificationRulesSettingsCommand = new AsyncCommand(_ =>
        {
            IsNotificationRulesPageOpen = false;
            return Task.CompletedTask;
        });
        OpenLaunchPathsSettingsCommand = new AsyncCommand(_ =>
        {
            IsNotificationRulesPageOpen = false;
            IsLaunchPathsPageOpen = true;
            return Task.CompletedTask;
        });
        CloseLaunchPathsSettingsCommand = new AsyncCommand(_ =>
        {
            IsLaunchPathsPageOpen = false;
            return Task.CompletedTask;
        });
        SaveNotificationRulesSettingsCommand = new AsyncCommand(SaveNotificationRulesSettingsAsync);
        BrowseArknightsGamePathCommand = new AsyncCommand(cancellationToken => BrowseLaunchPathAsync(GameDashboardKind.Arknights, launchScript: false, cancellationToken));
        BrowseArknightsScriptPathCommand = new AsyncCommand(cancellationToken => BrowseLaunchPathAsync(GameDashboardKind.Arknights, launchScript: true, cancellationToken));
        BrowseEndfieldGamePathCommand = new AsyncCommand(cancellationToken => BrowseLaunchPathAsync(GameDashboardKind.Endfield, launchScript: false, cancellationToken));
        BrowseEndfieldScriptPathCommand = new AsyncCommand(cancellationToken => BrowseLaunchPathAsync(GameDashboardKind.Endfield, launchScript: true, cancellationToken));
        BrowseWutheringWavesGamePathCommand = new AsyncCommand(cancellationToken => BrowseLaunchPathAsync(GameDashboardKind.WutheringWaves, launchScript: false, cancellationToken));
        BrowseWutheringWavesScriptPathCommand = new AsyncCommand(cancellationToken => BrowseLaunchPathAsync(GameDashboardKind.WutheringWaves, launchScript: true, cancellationToken));
        BrowseYihuanGamePathCommand = new AsyncCommand(cancellationToken => BrowseLaunchPathAsync(GameDashboardKind.Yihuan, launchScript: false, cancellationToken));
        BrowseYihuanScriptPathCommand = new AsyncCommand(cancellationToken => BrowseLaunchPathAsync(GameDashboardKind.Yihuan, launchScript: true, cancellationToken));
        SelectArknightsCommand = new AsyncCommand(_ =>
        {
            IsSettingsPageOpen = false;
            SelectGame(GameDashboardKind.Arknights);
            return Task.CompletedTask;
        });
        SelectEndfieldCommand = new AsyncCommand(_ =>
        {
            IsSettingsPageOpen = false;
            SelectGame(GameDashboardKind.Endfield);
            return Task.CompletedTask;
        });
        SelectWutheringWavesCommand = new AsyncCommand(_ =>
        {
            IsSettingsPageOpen = false;
            SelectGame(GameDashboardKind.WutheringWaves);
            return Task.CompletedTask;
        });
        SelectYihuanCommand = new AsyncCommand(_ =>
        {
            IsSettingsPageOpen = false;
            SelectGame(GameDashboardKind.Yihuan);
            return Task.CompletedTask;
        });

        _ = InitializeAsync();
    }

    public ObservableCollection<ArknightsPlayerBinding> PlayerBindings { get; } = new();

    public ObservableCollection<NotificationRuleViewModel> NotificationRules { get; } = new();

    public Window? OwnerWindow { get; set; }

    public event EventHandler? CredentialApplied;

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand RefreshAllCommand { get; }

    public AsyncCommand RefreshArknightsCommand { get; }

    public AsyncCommand RefreshEndfieldCommand { get; }

    public AsyncCommand RefreshWutheringWavesCommand { get; }

    public AsyncCommand RefreshYihuanCommand { get; }

    public AsyncCommand SignInCommand { get; }

    public AsyncCommand ScheduledSignInCommand { get; }

    public AsyncCommand EvaluateNotificationRulesCommand { get; }

    public AsyncCommand SaveCredentialCommand { get; }

    public AsyncCommand ClearCredentialCommand { get; }

    public AsyncCommand StartLoginCommand { get; }

    public AsyncCommand LaunchGameCommand { get; }

    public AsyncCommand LaunchScriptCommand { get; }

    public AsyncCommand BrowseGamePathCommand { get; }

    public AsyncCommand BrowseScriptPathCommand { get; }

    public AsyncCommand BrowseArknightsGamePathCommand { get; }

    public AsyncCommand BrowseArknightsScriptPathCommand { get; }

    public AsyncCommand BrowseEndfieldGamePathCommand { get; }

    public AsyncCommand BrowseEndfieldScriptPathCommand { get; }

    public AsyncCommand BrowseWutheringWavesGamePathCommand { get; }

    public AsyncCommand BrowseWutheringWavesScriptPathCommand { get; }

    public AsyncCommand BrowseYihuanGamePathCommand { get; }

    public AsyncCommand BrowseYihuanScriptPathCommand { get; }

    public AsyncCommand SelectArknightsCommand { get; }

    public AsyncCommand SelectEndfieldCommand { get; }

    public AsyncCommand SelectWutheringWavesCommand { get; }

    public AsyncCommand SelectYihuanCommand { get; }

    public AsyncCommand OpenSettingsCommand { get; }

    public AsyncCommand CloseSettingsCommand { get; }

    public AsyncCommand OpenNotificationRulesSettingsCommand { get; }

    public AsyncCommand CloseNotificationRulesSettingsCommand { get; }

    public AsyncCommand OpenLaunchPathsSettingsCommand { get; }

    public AsyncCommand CloseLaunchPathsSettingsCommand { get; }

    public AsyncCommand SaveNotificationRulesSettingsCommand { get; }

    public string Cred
    {
        get => _cred;
        set => SetProperty(ref _cred, value);
    }

    public string Token
    {
        get => _token;
        set => SetProperty(ref _token, value);
    }

    public string Cookie
    {
        get => _cookie;
        set => SetProperty(ref _cookie, value);
    }

    public string UserId
    {
        get => _userId;
        set => SetProperty(ref _userId, value);
    }

    public string DeviceId
    {
        get => _deviceId;
        set => SetProperty(ref _deviceId, value);
    }

    public ArknightsPlayerBinding? SelectedPlayerBinding
    {
        get => CurrentSelectedBinding();
        set
        {
            var changed = _selectedGame switch
            {
                GameDashboardKind.Arknights => SetProperty(ref _selectedArknightsBinding, value),
                GameDashboardKind.Endfield => SetProperty(ref _selectedEndfieldBinding, value),
                GameDashboardKind.WutheringWaves => SetProperty(ref _selectedWutheringWavesBinding, value),
                GameDashboardKind.Yihuan => SetProperty(ref _selectedYihuanBinding, value),
                _ => false
            };
            if (changed)
            {
                UpdateHeaderName();
                NotifyAccountChanged();
            }
        }
    }

    public bool AutoRefreshEnabled
    {
        get => GetAutoRefreshEnabled(_selectedGame);
        set
        {
            if (SetAutoRefreshEnabled(_selectedGame, value))
            {
                OnPropertyChanged(nameof(AutoRefreshSummary));
                OnPropertyChanged(nameof(ArknightsAutoRefreshEnabled));
                OnPropertyChanged(nameof(EndfieldAutoRefreshEnabled));
                OnPropertyChanged(nameof(WutheringWavesAutoRefreshEnabled));
                OnPropertyChanged(nameof(YihuanAutoRefreshEnabled));
                _ = SaveSettingsAsync();
            }
        }
    }

    public double AutoRefreshIntervalMinutes
    {
        get => GetAutoRefreshInterval(_selectedGame);
        set
        {
            var interval = Math.Clamp(double.IsNaN(value) ? 5 : value, 1, 180);
            if (SetAutoRefreshInterval(_selectedGame, interval))
            {
                OnPropertyChanged(nameof(AutoRefreshSummary));
                OnPropertyChanged(nameof(ArknightsAutoRefreshIntervalMinutes));
                OnPropertyChanged(nameof(EndfieldAutoRefreshIntervalMinutes));
                OnPropertyChanged(nameof(WutheringWavesAutoRefreshIntervalMinutes));
                OnPropertyChanged(nameof(YihuanAutoRefreshIntervalMinutes));
                _ = SaveSettingsAsync();
            }
        }
    }

    public double ArknightsAutoRefreshIntervalMinutes => _arknightsAutoRefreshIntervalMinutes;

    public double EndfieldAutoRefreshIntervalMinutes => _endfieldAutoRefreshIntervalMinutes;

    public double WutheringWavesAutoRefreshIntervalMinutes => _wutheringWavesAutoRefreshIntervalMinutes;

    public double YihuanAutoRefreshIntervalMinutes => _yihuanAutoRefreshIntervalMinutes;

    public bool ArknightsAutoRefreshEnabled => _arknightsAutoRefreshEnabled;

    public bool EndfieldAutoRefreshEnabled => _endfieldAutoRefreshEnabled;

    public bool WutheringWavesAutoRefreshEnabled => _wutheringWavesAutoRefreshEnabled;

    public bool YihuanAutoRefreshEnabled => _yihuanAutoRefreshEnabled;

    public string AutoRefreshSummary => AutoRefreshEnabled
        ? $"\u6BCF {AutoRefreshIntervalMinutes:0} \u5206\u949F\u5237\u65B0"
        : "\u81EA\u52A8\u5237\u65B0\u5DF2\u5173\u95ED";

    public bool IsSettingsPageOpen
    {
        get => _isSettingsPageOpen;
        set
        {
            if (SetProperty(ref _isSettingsPageOpen, value))
            {
                OnPropertyChanged(nameof(DashboardVisibility));
                OnPropertyChanged(nameof(SettingsVisibility));
                OnPropertyChanged(nameof(GeneralSettingsVisibility));
                OnPropertyChanged(nameof(NotificationRulesSettingsVisibility));
                OnPropertyChanged(nameof(LaunchPathsSettingsVisibility));
            }
        }
    }

    public Visibility DashboardVisibility => IsSettingsPageOpen ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SettingsVisibility => IsSettingsPageOpen ? Visibility.Visible : Visibility.Collapsed;

    public bool IsNotificationRulesPageOpen
    {
        get => _isNotificationRulesPageOpen;
        set
        {
            if (SetProperty(ref _isNotificationRulesPageOpen, value))
            {
                OnPropertyChanged(nameof(GeneralSettingsVisibility));
                OnPropertyChanged(nameof(NotificationRulesSettingsVisibility));
                OnPropertyChanged(nameof(LaunchPathsSettingsVisibility));
            }
        }
    }

    public bool IsLaunchPathsPageOpen
    {
        get => _isLaunchPathsPageOpen;
        set
        {
            if (SetProperty(ref _isLaunchPathsPageOpen, value))
            {
                OnPropertyChanged(nameof(GeneralSettingsVisibility));
                OnPropertyChanged(nameof(NotificationRulesSettingsVisibility));
                OnPropertyChanged(nameof(LaunchPathsSettingsVisibility));
            }
        }
    }

    public Visibility GeneralSettingsVisibility => IsSettingsPageOpen && !IsNotificationRulesPageOpen && !IsLaunchPathsPageOpen
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility NotificationRulesSettingsVisibility => IsSettingsPageOpen && IsNotificationRulesPageOpen
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility LaunchPathsSettingsVisibility => IsSettingsPageOpen && IsLaunchPathsPageOpen
        ? Visibility.Visible
        : Visibility.Collapsed;

    public bool NotificationRulesDirty
    {
        get => _notificationRulesDirty;
        private set
        {
            if (SetProperty(ref _notificationRulesDirty, value))
            {
                OnPropertyChanged(nameof(NotificationRulesSaveButtonText));
            }
        }
    }

    public string NotificationRulesSaveButtonText => NotificationRulesDirty
        ? "\u4FDD\u5B58\u66F4\u6539"
        : "\u5DF2\u4FDD\u5B58";

    public bool NotificationCooldownEnabled
    {
        get => _notificationCooldownEnabled;
        set
        {
            if (SetProperty(ref _notificationCooldownEnabled, value))
            {
                NotificationRulesDirty = true;
            }
        }
    }

    public double NotificationCooldownMinutes
    {
        get => _notificationCooldownMinutes;
        set
        {
            var normalized = NormalizeNotificationCooldownMinutes(value);
            if (SetProperty(ref _notificationCooldownMinutes, normalized))
            {
                NotificationRulesDirty = true;
            }
        }
    }

    public bool UseDarkTheme
    {
        get => _useDarkTheme;
        set
        {
            if (SetProperty(ref _useDarkTheme, value))
            {
                OnPropertyChanged(nameof(RootTheme));
                _ = SaveSettingsAsync();
            }
        }
    }

    public ElementTheme RootTheme => UseDarkTheme ? ElementTheme.Dark : ElementTheme.Light;

    public bool AutoSignEnabled
    {
        get => _autoSignEnabled;
        set
        {
            if (SetProperty(ref _autoSignEnabled, value))
            {
                _ = SaveSettingsAsync();
            }
        }
    }

    public bool DailyAutoSignEnabled
    {
        get => _dailyAutoSignEnabled;
        set
        {
            if (SetProperty(ref _dailyAutoSignEnabled, value))
            {
                OnPropertyChanged(nameof(DailyAutoSignSummary));
                _ = SaveSettingsAsync();
            }
        }
    }

    public string DailyAutoSignSummary => DailyAutoSignEnabled
        ? "每天 00:01 自动签到"
        : "每日自动签到已关闭";

    public bool ManualSignInAllGames
    {
        get => _manualSignInAllGames;
        set
        {
            if (SetProperty(ref _manualSignInAllGames, value))
            {
                OnPropertyChanged(nameof(SignInButtonText));
                _ = SaveSettingsAsync();
            }
        }
    }

    public string SignInButtonText => "\u7B7E\u5230";

    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set
        {
            if (SetProperty(ref _notificationsEnabled, value))
            {
                ConfigureNotificationChannels();
                _ = SaveSettingsAsync();
            }
        }
    }

    public bool ServerChanEnabled
    {
        get => _serverChanEnabled;
        set
        {
            if (SetProperty(ref _serverChanEnabled, value))
            {
                ConfigureNotificationChannels();
                _ = SaveSettingsAsync();
            }
        }
    }

    public string ServerChanSendKey
    {
        get => _serverChanSendKey;
        set
        {
            if (SetProperty(ref _serverChanSendKey, value))
            {
                ConfigureNotificationChannels();
                _ = SaveSettingsAsync();
            }
        }
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (SetProperty(ref _startWithWindows, value))
            {
                try
                {
                    _startupService.SetEnabled(value);
                }
                catch (Exception ex)
                {
                    SetStatus($"\u5F00\u673A\u81EA\u542F\u8BBE\u7F6E\u5931\u8D25\uFF1A{ex.Message}", InfoBarSeverity.Warning);
                }

                _ = SaveSettingsAsync();
            }
        }
    }

    public bool IsArknightsSelected
    {
        get => _selectedGame == GameDashboardKind.Arknights;
        set
        {
            if (value)
            {
                SelectGame(GameDashboardKind.Arknights);
            }
        }
    }

    public bool IsEndfieldSelected
    {
        get => _selectedGame == GameDashboardKind.Endfield;
        set
        {
            if (value)
            {
                SelectGame(GameDashboardKind.Endfield);
            }
        }
    }

    public bool IsWutheringWavesSelected
    {
        get => _selectedGame == GameDashboardKind.WutheringWaves;
        set
        {
            if (value)
            {
                SelectGame(GameDashboardKind.WutheringWaves);
            }
        }
    }

    public bool IsYihuanSelected
    {
        get => _selectedGame == GameDashboardKind.Yihuan;
        set
        {
            if (value)
            {
                SelectGame(GameDashboardKind.Yihuan);
            }
        }
    }

    public int GameSelectorThumbColumn => _selectedGame switch
    {
        GameDashboardKind.Arknights => 0,
        GameDashboardKind.Endfield => 1,
        GameDashboardKind.WutheringWaves => 2,
        GameDashboardKind.Yihuan => 3,
        _ => 0
    };

    public Visibility ArknightsDashboardVisibility => _selectedGame == GameDashboardKind.Arknights
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility EndfieldDashboardVisibility => _selectedGame == GameDashboardKind.Endfield
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility WutheringWavesDashboardVisibility => _selectedGame == GameDashboardKind.WutheringWaves
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility YihuanDashboardVisibility => _selectedGame == GameDashboardKind.Yihuan
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string SelectedGameTitle => GameTitle(_selectedGame);

    public string LaunchGameButtonText => "\u6E38\u620F";

    public string LaunchScriptButtonText => "\u811A\u672C";

    public string SelectedGameLaunchPath
    {
        get => GetLaunchPath(_selectedGame, launchScript: false);
        set
        {
            if (SetLaunchPath(_selectedGame, launchScript: false, value))
            {
                OnLaunchPathChanged();
            }
        }
    }

    public string SelectedScriptLaunchPath
    {
        get => GetLaunchPath(_selectedGame, launchScript: true);
        set
        {
            if (SetLaunchPath(_selectedGame, launchScript: true, value))
            {
                OnLaunchPathChanged();
            }
        }
    }

    public string ArknightsGameLaunchPath
    {
        get => _arknightsGamePath;
        set => SetLaunchPathAndSave(GameDashboardKind.Arknights, launchScript: false, value);
    }

    public string ArknightsScriptLaunchPath
    {
        get => _arknightsScriptPath;
        set => SetLaunchPathAndSave(GameDashboardKind.Arknights, launchScript: true, value);
    }

    public string EndfieldGameLaunchPath
    {
        get => _endfieldGamePath;
        set => SetLaunchPathAndSave(GameDashboardKind.Endfield, launchScript: false, value);
    }

    public string EndfieldScriptLaunchPath
    {
        get => _endfieldScriptPath;
        set => SetLaunchPathAndSave(GameDashboardKind.Endfield, launchScript: true, value);
    }

    public string WutheringWavesGameLaunchPath
    {
        get => _wutheringWavesGamePath;
        set => SetLaunchPathAndSave(GameDashboardKind.WutheringWaves, launchScript: false, value);
    }

    public string WutheringWavesScriptLaunchPath
    {
        get => _wutheringWavesScriptPath;
        set => SetLaunchPathAndSave(GameDashboardKind.WutheringWaves, launchScript: true, value);
    }

    public string YihuanGameLaunchPath
    {
        get => _yihuanGamePath;
        set => SetLaunchPathAndSave(GameDashboardKind.Yihuan, launchScript: false, value);
    }

    public string YihuanScriptLaunchPath
    {
        get => _yihuanScriptPath;
        set => SetLaunchPathAndSave(GameDashboardKind.Yihuan, launchScript: true, value);
    }

    public string AccountPanelSubtitle => _selectedGame switch
    {
        GameDashboardKind.Arknights => "\u7F57\u5FB7\u5C9B\u8D26\u53F7\u4E0E\u5237\u65B0\u8BBE\u7F6E",
        GameDashboardKind.Endfield => "\u7EC8\u672B\u5730\u8D26\u53F7\u4E0E\u5237\u65B0\u8BBE\u7F6E",
        GameDashboardKind.WutheringWaves => "\u5E93\u8857\u533A Token \u4E0E\u5237\u65B0\u8BBE\u7F6E",
        GameDashboardKind.Yihuan => "塔吉多 Token 与刷新设置",
        _ => string.Empty
    };

    public string CredFieldHeader => _selectedGame switch
    {
        GameDashboardKind.WutheringWaves => "\u5907\u6CE8\uFF08\u53EF\u7559\u7A7A\uFF09",
        GameDashboardKind.Yihuan => "老虎账号信息（自动填充，可留空）",
        _ => "Cred"
    };

    public string TokenFieldHeader => _selectedGame switch
    {
        GameDashboardKind.WutheringWaves => "\u5E93\u8857\u533A Token",
        GameDashboardKind.Yihuan => "塔吉多 Access Token",
        _ => "Token"
    };

    public string CookieFieldHeader => _selectedGame switch
    {
        GameDashboardKind.WutheringWaves => "\u5907\u7528 Cookie\uFF08\u53EF\u7559\u7A7A\uFF09",
        GameDashboardKind.Yihuan => "塔吉多 Refresh Token（可留空）",
        _ => "Cookie"
    };

    public string UserIdFieldHeader => _selectedGame switch
    {
        GameDashboardKind.WutheringWaves => "\u5E93\u8857\u533A User ID\uFF08\u81EA\u52A8\u586B\u5145\uFF09",
        GameDashboardKind.Yihuan => "塔吉多 UID（自动填充）",
        _ => "User ID"
    };

    public string DeviceIdFieldHeader => _selectedGame switch
    {
        GameDashboardKind.WutheringWaves => "\u8BBE\u5907 devCode",
        GameDashboardKind.Yihuan => "塔吉多 deviceId",
        _ => "Device ID"
    };

    public string DoctorName
    {
        get => _doctorName;
        private set => SetProperty(ref _doctorName, value);
    }

    public string AccountBadgeText => SelectedPlayerBinding is null
        ? "\u79BB\u7EBF"
        : "\u5DF2\u7ED1\u5B9A";

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public InfoBarSeverity StatusSeverity
    {
        get => _statusSeverity;
        private set => SetProperty(ref _statusSeverity, value);
    }

    public int SanityValue => _arknightsSnapshot?.Sanity.Current ?? 0;

    public int SanityMax => Math.Max(_arknightsSnapshot?.Sanity.Maximum ?? 1, 1);

    public string SanityText => FormatMeter(_arknightsSnapshot?.Sanity);

    public string SanityRecoveryText => FormatCompletion("\u56DE\u6EE1", _arknightsSnapshot?.Sanity.FullAt);

    public int DroneValue => _arknightsSnapshot?.Drones.Current ?? 0;

    public int DroneMax => Math.Max(_arknightsSnapshot?.Drones.Maximum ?? 1, 1);

    public string DroneText => FormatMeter(_arknightsSnapshot?.Drones);

    public string DroneRecoveryText => FormatCompletion("\u56DE\u6EE1", _arknightsSnapshot?.Drones.FullAt);

    public string TrainingOperatorName => _arknightsSnapshot?.TrainingRoom.OperatorName ?? "\u7A7A\u95F2";

    public string TrainingSkillText
    {
        get
        {
            var training = _arknightsSnapshot?.TrainingRoom;
            if (training is null || !training.IsTraining)
            {
                return "-";
            }

            return training.TargetSkillLevel is null
                ? training.SkillName
                : $"{training.SkillName} -> {training.TargetSkillLevel}";
        }
    }

    public string TrainingRemainingText => FormatRemaining(_arknightsSnapshot?.TrainingRoom.CompleteAt);

    public string TrainingCompleteAtText => FormatCompleteAt(string.Empty, _arknightsSnapshot?.TrainingRoom.CompleteAt);

    public int OrderValue => _arknightsSnapshot?.Building.Orders.Current ?? 0;

    public int OrderMax => Math.Max(_arknightsSnapshot?.Building.Orders.Maximum ?? 1, 1);

    public string OrderText => FormatProgress(_arknightsSnapshot?.Building.Orders);

    public string OrderCompletionText => FormatCompleteAt("\u4E0B\u4E00\u5355", _arknightsSnapshot?.Building.Orders.CompleteAt);

    public int ManufactureValue => _arknightsSnapshot?.Building.Manufacture.Current ?? 0;

    public int ManufactureMax => Math.Max(_arknightsSnapshot?.Building.Manufacture.Maximum ?? 1, 1);

    public string ManufactureText => FormatProgress(_arknightsSnapshot?.Building.Manufacture);

    public string ManufactureCompletionText => FormatCompleteAt("\u4E0B\u4E00\u4EF6", _arknightsSnapshot?.Building.Manufacture.CompleteAt);

    public string TiredOperatorsText => $"{_arknightsSnapshot?.Building.TiredOperators ?? 0}";

    public int AnnihilationValue => _arknightsSnapshot?.Annihilation.Current ?? 0;

    public int AnnihilationMax => _arknightsSnapshot?.Annihilation.Maximum ?? 1800;

    public string AnnihilationText => $"{AnnihilationValue}/{AnnihilationMax}";

    public string AnnihilationRefreshText => FormatRefreshAt(_arknightsSnapshot?.Annihilation.RefreshAt);

    public int SecurityServiceValue => _arknightsSnapshot?.SecurityService.Current ?? 0;

    public int SecurityServiceMax => _arknightsSnapshot?.SecurityService.Maximum ?? 24;

    public string SecurityServiceText => $"{SecurityServiceValue}/{SecurityServiceMax}";

    public int SecurityServiceStripValue => _arknightsSnapshot?.SecurityServiceStrips.Current ?? 0;

    public int SecurityServiceStripMax => _arknightsSnapshot?.SecurityServiceStrips.Maximum ?? 60;

    public string SecurityServiceStripText => $"{SecurityServiceStripValue}/{SecurityServiceStripMax}";

    public string SecurityServiceRefreshText => FormatRefreshAt(
        _arknightsSnapshot?.SecurityService.RefreshAt ?? _arknightsSnapshot?.SecurityServiceStrips.RefreshAt);

    public int EndfieldSanityValue => _endfieldSnapshot?.Sanity.Current ?? 0;

    public int EndfieldSanityMax => Math.Max(_endfieldSnapshot?.Sanity.Maximum ?? 1, 1);

    public string EndfieldSanityText => FormatMeter(_endfieldSnapshot?.Sanity);

    public string EndfieldSanityRecoveryText => FormatCompletion("\u56DE\u6EE1", _endfieldSnapshot?.Sanity.FullAt);

    public int EndfieldDailyActivityValue => _endfieldSnapshot?.DailyActivity.Current ?? 0;

    public int EndfieldDailyActivityMax => Math.Max(_endfieldSnapshot?.DailyActivity.Maximum ?? 1, 1);

    public string EndfieldDailyActivityText => FormatProgress(_endfieldSnapshot?.DailyActivity);

    public int EndfieldWeeklyTasksValue => _endfieldSnapshot?.WeeklyTasks.Current ?? 0;

    public int EndfieldWeeklyTasksMax => Math.Max(_endfieldSnapshot?.WeeklyTasks.Maximum ?? 1, 1);

    public string EndfieldWeeklyTasksText => FormatProgress(_endfieldSnapshot?.WeeklyTasks);

    public int EndfieldPassLevelValue => _endfieldSnapshot?.PassLevel.Current ?? 0;

    public int EndfieldPassLevelMax => Math.Max(_endfieldSnapshot?.PassLevel.Maximum ?? 1, 1);

    public string EndfieldPassLevelText => FormatProgress(_endfieldSnapshot?.PassLevel);

    public int WutheringWaveplatesValue => _wutheringWavesSnapshot?.Waveplates.Current ?? 0;

    public int WutheringWaveplatesMax => Math.Max(_wutheringWavesSnapshot?.Waveplates.Maximum ?? 1, 1);

    public string WutheringWaveplatesText => FormatWuwaResource(_wutheringWavesSnapshot?.Waveplates);

    public string WutheringWaveplatesRecoveryText => FormatCompletion("\u56DE\u6EE1", _wutheringWavesSnapshot?.Waveplates.RefreshAt);

    public int WutheringCrystalSolventValue => _wutheringWavesSnapshot?.CrystalSolvent.Current ?? 0;

    public int WutheringCrystalSolventMax => Math.Max(_wutheringWavesSnapshot?.CrystalSolvent.Maximum ?? 1, 1);

    public string WutheringCrystalSolventText => FormatWuwaResource(_wutheringWavesSnapshot?.CrystalSolvent);

    public int WutheringDailyActivityValue => _wutheringWavesSnapshot?.DailyActivity.Current ?? 0;

    public int WutheringDailyActivityMax => Math.Max(_wutheringWavesSnapshot?.DailyActivity.Maximum ?? 1, 1);

    public string WutheringDailyActivityText => FormatWuwaResource(_wutheringWavesSnapshot?.DailyActivity);

    public int WutheringWeeklyVoyageValue => _wutheringWavesSnapshot?.WeeklyVoyage.Current ?? 0;

    public int WutheringWeeklyVoyageMax => Math.Max(_wutheringWavesSnapshot?.WeeklyVoyage.Maximum ?? 1, 1);

    public string WutheringWeeklyVoyageText => FormatWuwaResource(_wutheringWavesSnapshot?.WeeklyVoyage);

    public int WutheringWeeklyBossValue => _wutheringWavesSnapshot?.WeeklyBoss.Current ?? 0;

    public int WutheringWeeklyBossMax => Math.Max(_wutheringWavesSnapshot?.WeeklyBoss.Maximum ?? 1, 1);

    public string WutheringWeeklyBossText => FormatWuwaResource(_wutheringWavesSnapshot?.WeeklyBoss);

    public int WutheringBattlePassValue => _wutheringWavesSnapshot?.BattlePassLevel.Current ?? 0;

    public int WutheringBattlePassMax => Math.Max(_wutheringWavesSnapshot?.BattlePassLevel.Maximum ?? 1, 1);

    public string WutheringBattlePassText => _wutheringWavesSnapshot?.BattlePassLevel.Maximum > 0
        ? FormatWuwaResource(_wutheringWavesSnapshot?.BattlePassLevel)
        : $"{_wutheringWavesSnapshot?.BattlePassLevel.Current ?? 0}";

    public string WutheringTowerResetText => FormatRefreshAt(_wutheringWavesSnapshot?.TowerResetAt);

    public string WutheringSeaResetText => FormatRefreshAt(_wutheringWavesSnapshot?.SeaResetAt);

    public string WutheringFinalBattleEndText => FormatEndAt("\u7ED3\u675F", _wutheringWavesSnapshot?.FinalBattleEndAt);

    public string WutheringSignInText => _wutheringWavesSnapshot?.HasSignedIn == true ? "\u4ECA\u65E5\u5DF2\u7B7E\u5230" : "\u4ECA\u65E5\u672A\u7B7E\u5230";

    public int YihuanNaturePixelsValue => _yihuanSnapshot?.NaturePixels.Current ?? 0;

    public int YihuanNaturePixelsMax => Math.Max(_yihuanSnapshot?.NaturePixels.Maximum ?? 1, 1);

    public string YihuanNaturePixelsText => FormatMeter(_yihuanSnapshot?.NaturePixels);

    public int YihuanCityVitalityValue => _yihuanSnapshot?.CityVitality.Current ?? 0;

    public int YihuanCityVitalityMax => Math.Max(_yihuanSnapshot?.CityVitality.Maximum ?? 1, 1);

    public string YihuanCityVitalityText => FormatMeter(_yihuanSnapshot?.CityVitality);

    public int YihuanDailyActivityValue => _yihuanSnapshot?.DailyActivity.Current ?? 0;

    public int YihuanDailyActivityMax => Math.Max(_yihuanSnapshot?.DailyActivity.Maximum ?? 1, 1);

    public string YihuanDailyActivityText => FormatProgress(_yihuanSnapshot?.DailyActivity);

    public int YihuanWeeklyBossValue => _yihuanSnapshot?.WeeklyBoss.Current ?? 0;

    public int YihuanWeeklyBossMax => Math.Max(_yihuanSnapshot?.WeeklyBoss.Maximum ?? 1, 1);

    public string YihuanWeeklyBossText => FormatProgress(_yihuanSnapshot?.WeeklyBoss);

    public string YihuanSignInText => _yihuanSnapshot?.HasSignedIn == true ? "今日已签到" : "今日未签到";

    private async Task InitializeAsync()
    {
        await LoadSettingsAsync(CancellationToken.None);

        var legacyCredential = await _credentialStore.LoadAsync(CancellationToken.None);
        var arknightsCredential = await _credentialStore.LoadAsync(CredentialScopeFor(GameDashboardKind.Arknights), CancellationToken.None);
        var endfieldCredential = await _credentialStore.LoadAsync(CredentialScopeFor(GameDashboardKind.Endfield), CancellationToken.None);
        var wutheringWavesCredential = await _credentialStore.LoadAsync(CredentialScopeFor(GameDashboardKind.WutheringWaves), CancellationToken.None);
        var yihuanCredential = await _credentialStore.LoadAsync(CredentialScopeFor(GameDashboardKind.Yihuan), CancellationToken.None);

        _arknightsCredential = arknightsCredential ?? legacyCredential ?? SklandCredential.Empty;
        _endfieldCredential = endfieldCredential ?? legacyCredential ?? SklandCredential.Empty;
        _wutheringWavesCredential = wutheringWavesCredential ?? SklandCredential.Empty;
        _yihuanCredential = yihuanCredential ?? SklandCredential.Empty;
        _credentialsLoaded = true;

        if (!_arknightsCredential.HasAnySecret && !_endfieldCredential.HasAnySecret && !_wutheringWavesCredential.HasAnySecret && !_yihuanCredential.HasAnySecret)
        {
            SetStatus("\u672A\u627E\u5230\u672C\u5730\u51ED\u636E\u3002", InfoBarSeverity.Informational);
            return;
        }

        ApplyCredential(GetCredentialFor(_selectedGame));
        SetStatus("\u5DF2\u52A0\u8F7D\u672C\u5730\u51ED\u636E\u3002", InfoBarSeverity.Success);

        await TryStartupSignInAsync(CancellationToken.None);
        await RefreshAllAsync(CancellationToken.None);
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        _isApplyingSettings = true;
        try
        {
            var settings = await _settingsStore.LoadAsync(cancellationToken);
            UseDarkTheme = settings.UseDarkTheme;
            AutoSignEnabled = settings.AutoSignEnabled;
            DailyAutoSignEnabled = settings.DailyAutoSignEnabled;
            ManualSignInAllGames = settings.ManualSignInAllGames;
            NotificationsEnabled = settings.NotificationsEnabled;
            ServerChanEnabled = settings.ServerChanEnabled;
            ServerChanSendKey = settings.ServerChanSendKey;
            NotificationCooldownEnabled = settings.NotificationCooldownEnabled;
            NotificationCooldownMinutes = settings.NotificationCooldownMinutes;
            _savedNotificationCooldownEnabled = NotificationCooldownEnabled;
            _savedNotificationCooldownMinutes = NotificationCooldownMinutes;
            _arknightsGamePath = settings.ArknightsGamePath;
            _arknightsScriptPath = settings.ArknightsScriptPath;
            _endfieldGamePath = settings.EndfieldGamePath;
            _endfieldScriptPath = settings.EndfieldScriptPath;
            _wutheringWavesGamePath = settings.WutheringWavesGamePath;
            _wutheringWavesScriptPath = settings.WutheringWavesScriptPath;
            _yihuanGamePath = settings.YihuanGamePath;
            _yihuanScriptPath = settings.YihuanScriptPath;
            InitializeNotificationRules(settings.NotificationRules);
            StartWithWindows = settings.StartWithWindows || _startupService.IsEnabled();
            _arknightsAutoRefreshEnabled = settings.ArknightsAutoRefreshEnabled;
            _endfieldAutoRefreshEnabled = settings.EndfieldAutoRefreshEnabled;
            _wutheringWavesAutoRefreshEnabled = settings.WutheringWavesAutoRefreshEnabled;
            _yihuanAutoRefreshEnabled = settings.YihuanAutoRefreshEnabled;
            _arknightsAutoRefreshIntervalMinutes = NormalizeAutoRefreshInterval(settings.ArknightsAutoRefreshIntervalMinutes);
            _endfieldAutoRefreshIntervalMinutes = NormalizeAutoRefreshInterval(settings.EndfieldAutoRefreshIntervalMinutes);
            _wutheringWavesAutoRefreshIntervalMinutes = NormalizeAutoRefreshInterval(settings.WutheringWavesAutoRefreshIntervalMinutes);
            _yihuanAutoRefreshIntervalMinutes = NormalizeAutoRefreshInterval(settings.YihuanAutoRefreshIntervalMinutes);
            OnPropertyChanged(nameof(AutoRefreshEnabled));
            OnPropertyChanged(nameof(AutoRefreshIntervalMinutes));
            OnPropertyChanged(nameof(AutoRefreshSummary));
            OnPropertyChanged(nameof(ArknightsAutoRefreshEnabled));
            OnPropertyChanged(nameof(EndfieldAutoRefreshEnabled));
            OnPropertyChanged(nameof(WutheringWavesAutoRefreshEnabled));
            OnPropertyChanged(nameof(YihuanAutoRefreshEnabled));
            OnPropertyChanged(nameof(ArknightsAutoRefreshIntervalMinutes));
            OnPropertyChanged(nameof(EndfieldAutoRefreshIntervalMinutes));
            OnPropertyChanged(nameof(WutheringWavesAutoRefreshIntervalMinutes));
            OnPropertyChanged(nameof(YihuanAutoRefreshIntervalMinutes));
            OnPropertyChanged(nameof(DailyAutoSignSummary));
            OnPropertyChanged(nameof(SelectedGameLaunchPath));
            OnPropertyChanged(nameof(SelectedScriptLaunchPath));
            OnPropertyChanged(nameof(ArknightsGameLaunchPath));
            OnPropertyChanged(nameof(ArknightsScriptLaunchPath));
            OnPropertyChanged(nameof(EndfieldGameLaunchPath));
            OnPropertyChanged(nameof(EndfieldScriptLaunchPath));
            OnPropertyChanged(nameof(WutheringWavesGameLaunchPath));
            OnPropertyChanged(nameof(WutheringWavesScriptLaunchPath));
            OnPropertyChanged(nameof(YihuanGameLaunchPath));
            OnPropertyChanged(nameof(YihuanScriptLaunchPath));
            ConfigureNotificationChannels();
        }
        finally
        {
            _isApplyingSettings = false;
            _settingsLoaded = true;
        }
    }

    private async Task<bool> SaveSettingsAsync(
        IReadOnlyList<NotificationRuleSetting>? notificationRules = null,
        bool? notificationCooldownEnabled = null,
        double? notificationCooldownMinutes = null)
    {
        if (!_settingsLoaded || _isApplyingSettings)
        {
            return false;
        }

        try
        {
            await _settingsStore.SaveAsync(
                new AppSettings(
                    UseDarkTheme,
                    AutoSignEnabled,
                    NotificationsEnabled,
                    StartWithWindows,
                    _arknightsAutoRefreshEnabled,
                    _endfieldAutoRefreshEnabled,
                    _wutheringWavesAutoRefreshEnabled,
                    _yihuanAutoRefreshEnabled,
                    _arknightsAutoRefreshIntervalMinutes,
                    _endfieldAutoRefreshIntervalMinutes,
                    _wutheringWavesAutoRefreshIntervalMinutes,
                    _yihuanAutoRefreshIntervalMinutes,
                    ManualSignInAllGames,
                    DailyAutoSignEnabled,
                    ServerChanEnabled,
                    ServerChanSendKey,
                    notificationCooldownEnabled ?? _savedNotificationCooldownEnabled,
                    notificationCooldownMinutes ?? _savedNotificationCooldownMinutes,
                    _arknightsGamePath,
                    _arknightsScriptPath,
                    _endfieldGamePath,
                    _endfieldScriptPath,
                    _wutheringWavesGamePath,
                    _wutheringWavesScriptPath,
                    _yihuanGamePath,
                    _yihuanScriptPath,
                    notificationRules ?? _savedNotificationRuleSettings),
                CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"\u8BBE\u7F6E\u4FDD\u5B58\u5931\u8D25\uFF1A{ex.Message}", InfoBarSeverity.Warning);
            return false;
        }
    }

    private async Task BrowseLaunchPathAsync(bool launchScript, CancellationToken cancellationToken)
    {
        await BrowseLaunchPathAsync(_selectedGame, launchScript, cancellationToken);
    }

    private async Task BrowseLaunchPathAsync(GameDashboardKind game, bool launchScript, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (OwnerWindow is null)
        {
            SetStatus("\u65E0\u6CD5\u6253\u5F00\u6587\u4EF6\u9009\u62E9\u5668\uFF1A\u7A97\u53E3\u672A\u5C31\u7EEA\u3002", InfoBarSeverity.Warning);
            return;
        }

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.Desktop
        };
        picker.FileTypeFilter.Add(".exe");
        picker.FileTypeFilter.Add(".lnk");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(OwnerWindow));

        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        if (SetLaunchPath(game, launchScript, file.Path))
        {
            OnLaunchPathChanged();
        }
    }

    private Task LaunchConfiguredProgramAsync(bool launchScript, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Environment.ExpandEnvironmentVariables(GetLaunchPath(_selectedGame, launchScript)).Trim();
        var targetName = launchScript ? "\u811A\u672C" : SelectedGameTitle;
        if (string.IsNullOrWhiteSpace(path))
        {
            SetStatus($"\u8BF7\u5148\u5728\u8BBE\u7F6E\u91CC\u914D\u7F6E{targetName}\u8DEF\u5F84\u3002", InfoBarSeverity.Warning);
            return Task.CompletedTask;
        }

        if (!File.Exists(path))
        {
            SetStatus($"{targetName}\u8DEF\u5F84\u4E0D\u5B58\u5728\uFF1A{path}", InfoBarSeverity.Warning);
            return Task.CompletedTask;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path) ?? Environment.CurrentDirectory
            });
            SetStatus($"\u5DF2\u542F\u52A8{targetName}\u3002", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus($"\u542F\u52A8{targetName}\u5931\u8D25\uFF1A{ex.Message}", InfoBarSeverity.Warning);
        }

        return Task.CompletedTask;
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            CommitCurrentCredentialFields();
            var credential = GetCredentialFor(_selectedGame);
            if (!credential.HasAnySecret)
            {
                SetStatus("\u7F3A\u5C11\u8D26\u53F7\u51ED\u636E\u3002", InfoBarSeverity.Warning);
                return;
            }

            await RefreshGameAsync(_selectedGame, credential, cancellationToken);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            CommitCurrentCredentialFields();
            if (!_arknightsCredential.HasAnySecret && !_endfieldCredential.HasAnySecret && !_wutheringWavesCredential.HasAnySecret && !_yihuanCredential.HasAnySecret)
            {
                SetStatus("\u7F3A\u5C11\u8D26\u53F7\u51ED\u636E\u3002", InfoBarSeverity.Warning);
                return;
            }

            var refreshed = 0;
            if (_arknightsCredential.HasAnySecret)
            {
                refreshed += await RefreshGameAsync(GameDashboardKind.Arknights, _arknightsCredential, cancellationToken, showStatus: false) ? 1 : 0;
            }

            if (_endfieldCredential.HasAnySecret)
            {
                refreshed += await RefreshGameAsync(GameDashboardKind.Endfield, _endfieldCredential, cancellationToken, showStatus: false) ? 1 : 0;
            }

            if (_wutheringWavesCredential.HasAnySecret)
            {
                refreshed += await RefreshGameAsync(GameDashboardKind.WutheringWaves, _wutheringWavesCredential, cancellationToken, showStatus: false) ? 1 : 0;
            }

            if (_yihuanCredential.HasAnySecret)
            {
                refreshed += await RefreshGameAsync(GameDashboardKind.Yihuan, _yihuanCredential, cancellationToken, showStatus: false) ? 1 : 0;
            }

            if (refreshed == 0)
            {
                SetStatus("\u6CA1\u6709\u627E\u5230\u5DF2\u7ED1\u5B9A\u7684\u6E38\u620F\u8D26\u53F7\u3002", InfoBarSeverity.Warning);
                return;
            }

            UpdateHeaderName();
            NotifySnapshotChanged();
            SetStatus($"\u5DF2\u5237\u65B0\u5168\u90E8\u6E38\u620F\uFF1A{DateTimeOffset.Now:HH:mm:ss}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async Task RefreshAutoGameAsync(GameDashboardKind game, CancellationToken cancellationToken)
    {
        try
        {
            CommitCurrentCredentialFields();
            var credential = GetCredentialFor(game);
            if (!credential.HasAnySecret)
            {
                return;
            }

            await RefreshGameAsync(game, credential, cancellationToken, showStatus: game == _selectedGame);
        }
        catch (Exception ex)
        {
            if (game == _selectedGame)
            {
                SetStatus(ex.Message, InfoBarSeverity.Error);
            }
        }
    }

    private async Task SignInManualAsync(CancellationToken cancellationToken)
    {
        if (ManualSignInAllGames)
        {
            await SignInAllAsync(cancellationToken, showNotification: NotificationsEnabled);
            return;
        }

        await SignInCurrentGameAsync(cancellationToken, showNotification: NotificationsEnabled);
    }

    private async Task SignInCurrentGameAsync(CancellationToken cancellationToken, bool showNotification)
    {
        try
        {
            CommitCurrentCredentialFields();
            var game = _selectedGame;
            var credential = GetCredentialFor(game);
            if (!credential.HasAnySecret)
            {
                SetStatus($"\u7B7E\u5230\u8DF3\u8FC7\uFF1A\u7F3A\u5C11{GameTitle(game)}\u8D26\u53F7\u51ED\u636E\u3002", InfoBarSeverity.Warning);
                return;
            }

            var results = await SignInGameAsync(game, credential, cancellationToken);
            if (results.Count == 0)
            {
                SetStatus($"\u6CA1\u6709\u627E\u5230\u53EF\u7B7E\u5230\u7684{GameTitle(game)}\u7ED1\u5B9A\u89D2\u8272\u3002", InfoBarSeverity.Warning);
                return;
            }

            await PublishSignInResultsAsync(results, showNotification, cancellationToken);
        }
        catch (Exception ex)
        {
            await PublishSignInFailureAsync(ex.Message, showNotification, cancellationToken);
        }
    }

    private async Task SignInAllAsync(CancellationToken cancellationToken)
    {
        await SignInAllAsync(cancellationToken, showNotification: NotificationsEnabled);
    }

    private async Task TryStartupSignInAsync(CancellationToken cancellationToken)
    {
        if (!AutoSignEnabled)
        {
            return;
        }

        await SignInAllAsync(cancellationToken, showNotification: NotificationsEnabled);
    }

    public bool TryReserveDailyAutoSign(DateTime now)
    {
        if (!_credentialsLoaded || !DailyAutoSignEnabled)
        {
            return false;
        }

        var dailyWindowStart = new TimeSpan(0, 1, 0);
        var dailyWindowEnd = new TimeSpan(0, 2, 0);
        if (now.TimeOfDay < dailyWindowStart || now.TimeOfDay >= dailyWindowEnd)
        {
            return false;
        }

        var today = DateOnly.FromDateTime(now);
        if (_lastDailyAutoSignDate == today)
        {
            return false;
        }

        _lastDailyAutoSignDate = today;
        return true;
    }

    private async Task SignInAllAsync(CancellationToken cancellationToken, bool showNotification)
    {
        try
        {
            CommitCurrentCredentialFields();
            if (!_arknightsCredential.HasAnySecret && !_endfieldCredential.HasAnySecret && !_wutheringWavesCredential.HasAnySecret && !_yihuanCredential.HasAnySecret)
            {
                SetStatus("\u81EA\u52A8\u7B7E\u5230\u8DF3\u8FC7\uFF1A\u7F3A\u5C11\u8D26\u53F7\u51ED\u636E\u3002", InfoBarSeverity.Warning);
                return;
            }

            var results = new List<SklandSignInResult>();
            if (_arknightsCredential.HasAnySecret)
            {
                results.AddRange(await SignInGameAsync(GameDashboardKind.Arknights, _arknightsCredential, cancellationToken));
            }

            if (_endfieldCredential.HasAnySecret)
            {
                results.AddRange(await SignInGameAsync(GameDashboardKind.Endfield, _endfieldCredential, cancellationToken));
            }

            if (_wutheringWavesCredential.HasAnySecret)
            {
                results.AddRange(await SignInGameAsync(GameDashboardKind.WutheringWaves, _wutheringWavesCredential, cancellationToken));
            }

            if (_yihuanCredential.HasAnySecret)
            {
                results.AddRange(await SignInGameAsync(GameDashboardKind.Yihuan, _yihuanCredential, cancellationToken));
            }

            if (results.Count == 0)
            {
                SetStatus("\u6CA1\u6709\u627E\u5230\u53EF\u7B7E\u5230\u7684\u7ED1\u5B9A\u89D2\u8272\u3002", InfoBarSeverity.Warning);
                return;
            }

            await PublishSignInResultsAsync(results, showNotification, cancellationToken);
        }
        catch (Exception ex)
        {
            await PublishSignInFailureAsync(ex.Message, showNotification, cancellationToken);
        }
    }

    private async Task PublishSignInResultsAsync(
        IReadOnlyList<SklandSignInResult> results,
        bool showNotification,
        CancellationToken cancellationToken)
    {
        var failed = results.Count(result => result.IsFailure);
        var alreadySigned = results.Count(result => result.State == SklandSignInState.AlreadySigned);
        var succeeded = results.Count(result => result.State == SklandSignInState.Success);
        var title = failed > 0
            ? "\u6E38\u620F\u7B7E\u5230\u5B8C\u6210\uFF0C\u90E8\u5206\u5931\u8D25"
            : succeeded == 0 && alreadySigned > 0
                ? "\u4ECA\u65E5\u5DF2\u7B7E\u5230"
                : "\u6E38\u620F\u7B7E\u5230\u5B8C\u6210";
        var message = BuildSignInSummary(results);

        SetStatus(message, failed > 0 ? InfoBarSeverity.Warning : InfoBarSeverity.Success);
        if (showNotification)
        {
            await _notificationService.ShowAsync(title, message, cancellationToken);
        }
    }

    private async Task PublishSignInFailureAsync(
        string reason,
        bool showNotification,
        CancellationToken cancellationToken)
    {
        var message = $"\u7B7E\u5230\u5931\u8D25\uFF1A{reason}";
        SetStatus(message, InfoBarSeverity.Error);
        if (showNotification)
        {
            await _notificationService.ShowAsync("\u6E38\u620F\u7B7E\u5230\u5931\u8D25", message, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<SklandSignInResult>> SignInGameAsync(
        GameDashboardKind game,
        SklandCredential credential,
        CancellationToken cancellationToken)
    {
        await EnsureBindingsAsync(game, credential, cancellationToken);
        var bindings = BindingCache(game).ToArray();
        if (bindings.Length == 0)
        {
            return Array.Empty<SklandSignInResult>();
        }

        if (game == GameDashboardKind.WutheringWaves)
        {
            var kuroResults = new List<SklandSignInResult>();
            foreach (var binding in bindings)
            {
                kuroResults.Add(await _kuroSignInService.SignInAsync(credential, binding, cancellationToken));
            }

            return kuroResults;
        }

        if (game == GameDashboardKind.Yihuan)
        {
            return await _tajiduoSignInService.SignInAsync(credential, bindings, cancellationToken);
        }

        return await _signInService.SignInAsync(credential, game, bindings, cancellationToken);
    }

    private async Task<bool> RefreshGameAsync(
        GameDashboardKind game,
        SklandCredential credential,
        CancellationToken cancellationToken,
        bool showStatus = true)
    {
        await EnsureBindingsAsync(game, credential, cancellationToken);
        credential = GetCredentialFor(game);

        var selectedBinding = GetSelectedBinding(game);
        if (selectedBinding is null)
        {
            if (showStatus)
            {
                SetStatus($"\u6CA1\u6709\u627E\u5230\u5DF2\u7ED1\u5B9A\u7684{GameTitle(game)}\u8D26\u53F7\u3002", InfoBarSeverity.Warning);
            }

            return false;
        }

        DateTimeOffset updatedAt;
        if (game == GameDashboardKind.Arknights)
        {
            _arknightsSnapshot = await _monitor.GetStatusAsync(credential, selectedBinding, cancellationToken);
            updatedAt = _arknightsSnapshot.UpdatedAt;
        }
        else if (game == GameDashboardKind.Endfield)
        {
            _endfieldSnapshot = await _monitor.GetEndfieldStatusAsync(credential, selectedBinding, cancellationToken);
            updatedAt = _endfieldSnapshot.UpdatedAt;
        }
        else if (game == GameDashboardKind.WutheringWaves)
        {
            _wutheringWavesSnapshot = await _kuroMonitor.GetStatusAsync(credential, selectedBinding, cancellationToken);
            updatedAt = _wutheringWavesSnapshot.UpdatedAt;
        }
        else
        {
            _yihuanSnapshot = await _tajiduoMonitor.GetStatusAsync(credential, selectedBinding, cancellationToken);
            updatedAt = _yihuanSnapshot.UpdatedAt;
        }

        if (game == _selectedGame)
        {
            SyncDisplayedBindings();
            UpdateHeaderName();
        }

        NotifySnapshotChanged();
        if (showStatus)
        {
            SetStatus($"\u5DF2\u5237\u65B0{GameTitle(game)}\uFF1A{updatedAt:HH:mm:ss}", InfoBarSeverity.Success);
        }

        await EvaluateNotificationRulesAsync(cancellationToken);
        return true;
    }

    private async Task SaveCredentialAsync(CancellationToken cancellationToken)
    {
        CommitCurrentCredentialFields();
        await SaveCredentialForGameAsync(_selectedGame, GetCredentialFor(_selectedGame), cancellationToken);
        SetStatus($"{SelectedGameTitle}\u51ED\u636E\u5DF2\u4FDD\u5B58\u3002", InfoBarSeverity.Success);
    }

    private async Task EnsureBindingsAsync(GameDashboardKind game, SklandCredential credential, CancellationToken cancellationToken)
    {
        var target = BindingCache(game);
        if (target.Count == 0)
        {
            if (game == GameDashboardKind.WutheringWaves)
            {
                var bindings = await _kuroMonitor.GetBindingsAsync(credential, cancellationToken);
                var resolvedUserId = bindings.FirstOrDefault()?.UserId;
                if (!string.IsNullOrWhiteSpace(resolvedUserId) &&
                    !string.Equals(credential.UserId, resolvedUserId, StringComparison.Ordinal))
                {
                    credential = credential with { UserId = resolvedUserId };
                    SetCredentialFor(game, credential);
                    await SaveCredentialForGameAsync(game, credential, cancellationToken);
                    if (game == _selectedGame)
                    {
                        ApplyCredential(credential);
                    }
                }

                target.Clear();
                target.AddRange(bindings);
            }
            else if (game == GameDashboardKind.Yihuan)
            {
                var bindings = await _tajiduoMonitor.GetBindingsAsync(credential, cancellationToken);
                target.Clear();
                target.AddRange(bindings);
            }
            else
            {
                var bindingResult = await _monitor.GetBindingsAsync(credential, AppCodeFor(game), cancellationToken);
                if (!string.IsNullOrWhiteSpace(bindingResult.ResolvedUserId) &&
                    !string.Equals(credential.UserId, bindingResult.ResolvedUserId, StringComparison.Ordinal))
                {
                    credential = credential with { UserId = bindingResult.ResolvedUserId };
                    SetCredentialFor(game, credential);
                    await SaveCredentialForGameAsync(game, credential, cancellationToken);
                    if (game == _selectedGame)
                    {
                        ApplyCredential(credential);
                    }
                }

                target.Clear();
                target.AddRange(bindingResult.Bindings);
            }
        }

        if (game == _selectedGame)
        {
            SyncDisplayedBindings();
        }
        else
        {
            EnsureSelectedBinding(game);
        }
    }

    private async Task ClearCredentialAsync(CancellationToken cancellationToken)
    {
        var game = _selectedGame;
        await _credentialStore.ClearAsync(CredentialScopeFor(game), cancellationToken);
        SetCredentialFor(game, SklandCredential.Empty);
        ApplyCredential(SklandCredential.Empty);
        BindingCache(game).Clear();
        PlayerBindings.Clear();
        SetSelectedBinding(game, null);
        OnPropertyChanged(nameof(SelectedPlayerBinding));
        if (game == GameDashboardKind.Arknights)
        {
            _arknightsSnapshot = null;
        }
        else if (game == GameDashboardKind.Endfield)
        {
            _endfieldSnapshot = null;
        }
        else if (game == GameDashboardKind.WutheringWaves)
        {
            _wutheringWavesSnapshot = null;
        }
        else
        {
            _yihuanSnapshot = null;
        }

        DoctorName = "\u672A\u767B\u5F55";
        NotifySnapshotChanged();
        SetStatus($"{SelectedGameTitle}\u672C\u5730\u51ED\u636E\u5DF2\u6E05\u9664\u3002", InfoBarSeverity.Success);
    }

    private async Task StartLoginAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (OwnerWindow is null)
            {
                SetStatus("\u767B\u5F55\u7A97\u53E3\u5C1A\u672A\u5C31\u7EEA\u3002", InfoBarSeverity.Warning);
                return;
            }

            CommitCurrentCredentialFields();
            var game = _selectedGame;
            if (game == GameDashboardKind.WutheringWaves)
            {
                var kuroCredential = await _kuroLoginService.LoginAsync(OwnerWindow, GetCredentialFor(game), cancellationToken);
                if (kuroCredential is null)
                {
                    SetStatus("\u5DF2\u53D6\u6D88\u5E93\u8857\u533A\u767B\u5F55\u3002", InfoBarSeverity.Informational);
                    return;
                }

                SetCredentialFor(game, kuroCredential);
                ApplyCredential(kuroCredential);
                await SaveCredentialForGameAsync(game, kuroCredential, cancellationToken);
                BindingCache(game).Clear();
                PlayerBindings.Clear();
                SetSelectedBinding(game, null);
                _wutheringWavesSnapshot = null;
                OnPropertyChanged(nameof(SelectedPlayerBinding));
                NotifySnapshotChanged();
                SetStatus("\u5E93\u8857\u533A\u767B\u5F55\u51ED\u636E\u5DF2\u4FDD\u5B58\uFF0C\u6B63\u5728\u9A8C\u8BC1\u9E23\u6F6E\u6570\u636E\u3002", InfoBarSeverity.Success);
                await RefreshGameAsync(game, kuroCredential, cancellationToken);
                return;
            }

            if (game == GameDashboardKind.Yihuan)
            {
                var tajiduoCredential = await _tajiduoLoginService.LoginAsync(OwnerWindow, GetCredentialFor(game), cancellationToken);
                if (tajiduoCredential is null)
                {
                    SetStatus("已取消塔吉多登录。", InfoBarSeverity.Informational);
                    return;
                }

                SetCredentialFor(game, tajiduoCredential);
                ApplyCredential(tajiduoCredential);
                await SaveCredentialForGameAsync(game, tajiduoCredential, cancellationToken);
                BindingCache(game).Clear();
                PlayerBindings.Clear();
                SetSelectedBinding(game, null);
                _yihuanSnapshot = null;
                OnPropertyChanged(nameof(SelectedPlayerBinding));
                NotifySnapshotChanged();
                SetStatus("塔吉多登录凭据已保存，正在验证异环数据。", InfoBarSeverity.Success);
                await RefreshGameAsync(game, tajiduoCredential, cancellationToken);
                return;
            }

            var previousCredential = GetCredentialFor(game);
            var credential = await _loginService.LoginAsync(OwnerWindow, previousCredential, cancellationToken);
            if (credential is null)
            {
                SetStatus("\u5DF2\u53D6\u6D88\u767B\u5F55\u3002", InfoBarSeverity.Informational);
                return;
            }

            var credentialChanged =
                !string.Equals(previousCredential.Cred, credential.Cred, StringComparison.Ordinal) ||
                !string.Equals(previousCredential.UserId, credential.UserId, StringComparison.Ordinal);

            SetCredentialFor(game, credential);
            ApplyCredential(credential);
            await SaveCredentialForGameAsync(game, credential, cancellationToken);
            if (credentialChanged)
            {
                BindingCache(game).Clear();
                PlayerBindings.Clear();
                SetSelectedBinding(game, null);
                OnPropertyChanged(nameof(SelectedPlayerBinding));
                if (game == GameDashboardKind.Arknights)
                {
                    _arknightsSnapshot = null;
                }
                else if (game == GameDashboardKind.Endfield)
                {
                    _endfieldSnapshot = null;
                }
                else
                {
                    _wutheringWavesSnapshot = null;
                }

                NotifySnapshotChanged();
            }

            SetStatus("\u767B\u5F55\u51ED\u636E\u5DF2\u4FDD\u5B58\uFF0C\u6B63\u5728\u9A8C\u8BC1\u6570\u636E\u6293\u53D6\u3002", InfoBarSeverity.Success);
            await RefreshGameAsync(game, credential, cancellationToken);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, InfoBarSeverity.Error);
        }
    }

    private SklandCredential BuildCredential()
    {
        return new SklandCredential(
            Cred.Trim(),
            Token.Trim(),
            Cookie.Trim(),
            UserId.Trim(),
            string.IsNullOrWhiteSpace(DeviceId) ? Guid.NewGuid().ToString("N") : DeviceId.Trim(),
            DateTimeOffset.Now);
    }

    private void CommitCurrentCredentialFields()
    {
        SetCredentialFor(_selectedGame, BuildCredential());
    }

    private void ApplyCredential(SklandCredential credential)
    {
        Cred = credential.Cred;
        Token = credential.Token;
        Cookie = credential.Cookie;
        UserId = credential.UserId ?? string.Empty;
        DeviceId = string.IsNullOrWhiteSpace(credential.DeviceId)
            ? Guid.NewGuid().ToString("N")
            : credential.DeviceId;
        CredentialApplied?.Invoke(this, EventArgs.Empty);
    }

    private void SelectGame(GameDashboardKind game)
    {
        if (_selectedGame == game)
        {
            return;
        }

        CommitCurrentCredentialFields();
        _selectedGame = game;
        ApplyCredential(GetCredentialFor(game));
        SyncDisplayedBindings();
        UpdateHeaderName();
        NotifyGameChanged();
        NotifySnapshotChanged();
    }

    private async Task SaveCredentialForGameAsync(
        GameDashboardKind game,
        SklandCredential credential,
        CancellationToken cancellationToken)
    {
        await _credentialStore.SaveAsync(CredentialScopeFor(game), credential, cancellationToken);
    }

    private static string CredentialScopeFor(GameDashboardKind game)
    {
        return game switch
        {
            GameDashboardKind.Arknights => "arknights",
            GameDashboardKind.Endfield => "endfield",
            GameDashboardKind.WutheringWaves => "wutheringwaves",
            GameDashboardKind.Yihuan => "yihuan",
            _ => "default"
        };
    }

    private SklandCredential GetCredentialFor(GameDashboardKind game)
    {
        return game switch
        {
            GameDashboardKind.Arknights => _arknightsCredential,
            GameDashboardKind.Endfield => _endfieldCredential,
            GameDashboardKind.WutheringWaves => _wutheringWavesCredential,
            GameDashboardKind.Yihuan => _yihuanCredential,
            _ => SklandCredential.Empty
        };
    }

    private void SetCredentialFor(GameDashboardKind game, SklandCredential credential)
    {
        if (game == GameDashboardKind.Arknights)
        {
            _arknightsCredential = credential;
        }
        else if (game == GameDashboardKind.Endfield)
        {
            _endfieldCredential = credential;
        }
        else if (game == GameDashboardKind.WutheringWaves)
        {
            _wutheringWavesCredential = credential;
        }
        else if (game == GameDashboardKind.Yihuan)
        {
            _yihuanCredential = credential;
        }
    }

    private static string AppCodeFor(GameDashboardKind game)
    {
        return game == GameDashboardKind.Arknights ? ArknightsAppCode : EndfieldAppCode;
    }

    private List<ArknightsPlayerBinding> BindingCache(GameDashboardKind game)
    {
        return game switch
        {
            GameDashboardKind.Arknights => _arknightsBindings,
            GameDashboardKind.Endfield => _endfieldBindings,
            GameDashboardKind.WutheringWaves => _wutheringWavesBindings,
            GameDashboardKind.Yihuan => _yihuanBindings,
            _ => _arknightsBindings
        };
    }

    private List<ArknightsPlayerBinding> CurrentBindingCache()
    {
        return BindingCache(_selectedGame);
    }

    private ArknightsPlayerBinding? CurrentSelectedBinding()
    {
        return GetSelectedBinding(_selectedGame);
    }

    private ArknightsPlayerBinding? GetSelectedBinding(GameDashboardKind game)
    {
        return game switch
        {
            GameDashboardKind.Arknights => _selectedArknightsBinding,
            GameDashboardKind.Endfield => _selectedEndfieldBinding,
            GameDashboardKind.WutheringWaves => _selectedWutheringWavesBinding,
            GameDashboardKind.Yihuan => _selectedYihuanBinding,
            _ => null
        };
    }

    private void SetSelectedBinding(GameDashboardKind game, ArknightsPlayerBinding? binding)
    {
        if (game == GameDashboardKind.Arknights)
        {
            _selectedArknightsBinding = binding;
        }
        else if (game == GameDashboardKind.Endfield)
        {
            _selectedEndfieldBinding = binding;
        }
        else if (game == GameDashboardKind.WutheringWaves)
        {
            _selectedWutheringWavesBinding = binding;
        }
        else if (game == GameDashboardKind.Yihuan)
        {
            _selectedYihuanBinding = binding;
        }
    }

    private static string GameTitle(GameDashboardKind game)
    {
        return game switch
        {
            GameDashboardKind.Arknights => "\u660E\u65E5\u65B9\u821F",
            GameDashboardKind.Endfield => "\u7EC8\u672B\u5730",
            GameDashboardKind.WutheringWaves => "\u9E23\u6F6E",
            GameDashboardKind.Yihuan => "异环",
            _ => string.Empty
        };
    }

    private void SyncDisplayedBindings()
    {
        var currentUid = CurrentSelectedBinding()?.Uid;
        var bindings = CurrentBindingCache();

        PlayerBindings.Clear();
        foreach (var binding in bindings)
        {
            PlayerBindings.Add(binding);
        }

        var selected = PlayerBindings.FirstOrDefault(binding => binding.Uid == currentUid) ??
            PlayerBindings.FirstOrDefault();
        SetSelectedBinding(_selectedGame, selected);
        OnPropertyChanged(nameof(SelectedPlayerBinding));
        NotifyAccountChanged();
    }

    private void EnsureSelectedBinding(GameDashboardKind game)
    {
        var selected = GetSelectedBinding(game);
        var bindings = BindingCache(game);
        if (selected is not null && bindings.Any(binding => binding.Uid == selected.Uid))
        {
            return;
        }

        SetSelectedBinding(game, bindings.FirstOrDefault());
    }

    private void UpdateHeaderName()
    {
        DoctorName = _selectedGame switch
        {
            GameDashboardKind.Arknights => _arknightsSnapshot?.DoctorName ?? SelectedPlayerBinding?.NickName ?? "\u672A\u767B\u5F55",
            GameDashboardKind.Endfield => _endfieldSnapshot?.PlayerName ?? SelectedPlayerBinding?.NickName ?? "\u672A\u767B\u5F55",
            GameDashboardKind.WutheringWaves => _wutheringWavesSnapshot?.PlayerName ?? SelectedPlayerBinding?.NickName ?? "\u672A\u767B\u5F55",
            GameDashboardKind.Yihuan => _yihuanSnapshot?.PlayerName ?? SelectedPlayerBinding?.NickName ?? "\u672A\u767B\u5F55",
            _ => "\u672A\u767B\u5F55"
        };
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusSeverity = severity;
        StatusMessage = message;
    }

    private void ConfigureNotificationChannels()
    {
        _notificationService.Configure(new AppNotificationOptions(
            NotificationsEnabled,
            ServerChanEnabled,
            ServerChanSendKey));
    }

    private void InitializeNotificationRules(IReadOnlyList<NotificationRuleSetting>? settings)
    {
        foreach (var rule in NotificationRules)
        {
            rule.PropertyChanged -= NotificationRule_OnPropertyChanged;
        }

        NotificationRules.Clear();
        var settingsByMetric = (settings ?? Array.Empty<NotificationRuleSetting>())
            .GroupBy(setting => string.IsNullOrWhiteSpace(setting.MetricId) ? setting.Id : setting.MetricId)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (var metric in NotificationMetricDefinitions)
        {
            settingsByMetric.TryGetValue(metric.Id, out var setting);
            var rule = new NotificationRuleViewModel(
                metric.Id,
                metric.GameTitle,
                metric.MetricTitle,
                metric.Id,
                metric.DefaultCategory,
                setting);
            rule.PropertyChanged += NotificationRule_OnPropertyChanged;
            NotificationRules.Add(rule);
        }

        _savedNotificationRuleSettings = NotificationRules.Select(rule => rule.ToSetting()).ToArray();
        NotificationRulesDirty = false;
    }

    private void NotificationRule_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingSettings)
        {
            return;
        }

        NotificationRulesDirty = true;
    }

    private async Task SaveNotificationRulesSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = NotificationRules.Select(rule => rule.ToSetting()).ToArray();
        var saved = await SaveSettingsAsync(
            settings,
            NotificationCooldownEnabled,
            NotificationCooldownMinutes);
        if (!saved)
        {
            return;
        }

        _savedNotificationRuleSettings = settings;
        _savedNotificationCooldownEnabled = NotificationCooldownEnabled;
        _savedNotificationCooldownMinutes = NotificationCooldownMinutes;
        NotificationRulesDirty = false;
        SetStatus("\u63D0\u9192\u89C4\u5219\u5DF2\u4FDD\u5B58\u3002", InfoBarSeverity.Success);
    }

    private async Task EvaluateNotificationRulesAsync(CancellationToken cancellationToken)
    {
        if (NotificationRules.Count == 0)
        {
            return;
        }

        var metrics = BuildNotificationMetricStates()
            .ToDictionary(metric => metric.Id, StringComparer.Ordinal);
        if (metrics.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        foreach (var rule in NotificationRules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!rule.Enabled || !metrics.TryGetValue(rule.MetricId, out var metric))
            {
                continue;
            }

            var notificationKey = TryCreateNotificationKey(rule, metric, now);
            if (notificationKey is null || !TryReserveNotification(notificationKey, now))
            {
                continue;
            }

            var message = BuildRuleNotificationMessage(rule, metric, now);
            await _notificationService.ShowAsync(
                $"{metric.GameTitle}提醒：{metric.MetricTitle}",
                message,
                cancellationToken);
        }
    }

    private bool TryReserveNotification(string notificationKey, DateTimeOffset now)
    {
        if (!_sentNotificationTimes.TryGetValue(notificationKey, out var lastSentAt))
        {
            _sentNotificationTimes[notificationKey] = now;
            return true;
        }

        if (NotificationCooldownEnabled)
        {
            var cooldown = TimeSpan.FromMinutes(NotificationCooldownMinutes);
            if (now - lastSentAt < cooldown)
            {
                return false;
            }
        }

        _sentNotificationTimes[notificationKey] = now;
        return true;
    }

    private static string? TryCreateNotificationKey(
        NotificationRuleViewModel rule,
        NotificationMetricState metric,
        DateTimeOffset now)
    {
        return rule.Category switch
        {
            "日常" => TryCreateDailyNotificationKey(rule, metric, now),
            "周常" => TryCreateWeeklyNotificationKey(rule, metric, now),
            "周期" => TryCreatePeriodicNotificationKey(rule, metric, now),
            _ => null
        };
    }

    private static string? TryCreateDailyNotificationKey(
        NotificationRuleViewModel rule,
        NotificationMetricState metric,
        DateTimeOffset now)
    {
        if (!Compare(metric.Current, rule.Operator, rule.Threshold))
        {
            return null;
        }

        var date = DateOnly.FromDateTime(now.LocalDateTime);
        if (rule.UseScheduledTime && !IsTimeWindow(now, (int)rule.Hour, (int)rule.Minute, TimeSpan.FromMinutes(2)))
        {
            return null;
        }

        var mode = rule.UseScheduledTime ? $"{rule.Hour:00}:{rule.Minute:00}" : "threshold";
        return $"daily:{rule.Id}:{date:yyyyMMdd}:{mode}";
    }

    private static string? TryCreateWeeklyNotificationKey(
        NotificationRuleViewModel rule,
        NotificationMetricState metric,
        DateTimeOffset now)
    {
        if (ToDayOfWeek(rule.Weekday) != now.DayOfWeek ||
            !IsTimeWindow(now, (int)rule.Hour, (int)rule.Minute, TimeSpan.FromMinutes(2)))
        {
            return null;
        }

        if (rule.RequireThresholdForWeekly && !Compare(metric.Current, rule.Operator, rule.Threshold))
        {
            return null;
        }

        return $"weekly:{rule.Id}:{now.LocalDateTime:yyyyMMdd}:{rule.Hour:00}:{rule.Minute:00}:{rule.RequireThresholdForWeekly}";
    }

    private static string? TryCreatePeriodicNotificationKey(
        NotificationRuleViewModel rule,
        NotificationMetricState metric,
        DateTimeOffset now)
    {
        if (metric.PeriodAt is null)
        {
            return null;
        }

        if (rule.RequireThresholdForPeriod && !Compare(metric.Current, rule.Operator, rule.Threshold))
        {
            return null;
        }

        var target = metric.PeriodAt.Value.AddDays(-(int)rule.DaysBefore);
        if (now < target || now > metric.PeriodAt.Value)
        {
            return null;
        }

        return $"period:{rule.Id}:{metric.PeriodAt.Value:yyyyMMddHHmm}:{rule.DaysBefore:0}:{rule.RequireThresholdForPeriod}";
    }

    private static string BuildRuleNotificationMessage(
        NotificationRuleViewModel rule,
        NotificationMetricState metric,
        DateTimeOffset now)
    {
        var valueText = metric.Maximum > 0
            ? $"{metric.Current}/{metric.Maximum}"
            : $"{metric.Current}";

        if (rule.Category == "周期" && metric.PeriodAt is not null)
        {
            var remaining = metric.PeriodAt.Value - now;
            var periodText = $"{metric.PeriodLabel} {FormatClockWithDay(metric.PeriodAt.Value)} · 还需 {FormatDuration(remaining)}";
            return rule.RequireThresholdForPeriod
                ? $"{periodText}\n当前值 {valueText}，规则：{rule.Operator}{rule.Threshold:0}"
                : periodText;
        }

        var timeText = rule.Category == "日常" && !rule.UseScheduledTime
            ? "当前"
            : $"{rule.Weekday} {rule.Hour:00}:{rule.Minute:00}";
        if (rule.Category == "日常" && rule.UseScheduledTime)
        {
            timeText = $"{rule.Hour:00}:{rule.Minute:00}";
        }

        if (rule.Category == "周常" && !rule.RequireThresholdForWeekly)
        {
            return $"{timeText} 检查命中\n当前值 {valueText}";
        }

        return $"{timeText} 检查命中\n当前值 {valueText}，规则：{rule.Operator}{rule.Threshold:0}";
    }

    private IEnumerable<NotificationMetricState> BuildNotificationMetricStates()
    {
        if (_arknightsSnapshot is not null)
        {
            yield return Metric("arknights.sanity", "明日方舟", "理智", _arknightsSnapshot.Sanity.Current, _arknightsSnapshot.Sanity.Maximum, _arknightsSnapshot.Sanity.FullAt, "回满");
            yield return Metric("arknights.drones", "明日方舟", "无人机", _arknightsSnapshot.Drones.Current, _arknightsSnapshot.Drones.Maximum, _arknightsSnapshot.Drones.FullAt, "回满");
            yield return Metric("arknights.orders", "明日方舟", "订单进度", _arknightsSnapshot.Building.Orders.Current, _arknightsSnapshot.Building.Orders.Maximum, _arknightsSnapshot.Building.Orders.CompleteAt, "下一单");
            yield return Metric("arknights.manufacture", "明日方舟", "制造进度", _arknightsSnapshot.Building.Manufacture.Current, _arknightsSnapshot.Building.Manufacture.Maximum, _arknightsSnapshot.Building.Manufacture.CompleteAt, "下一件");
            yield return Metric("arknights.tired", "明日方舟", "干员疲劳", _arknightsSnapshot.Building.TiredOperators, 0, null, "刷新");
            yield return Metric("arknights.annihilation", "明日方舟", "每周剿灭", _arknightsSnapshot.Annihilation.Current, _arknightsSnapshot.Annihilation.Maximum, _arknightsSnapshot.Annihilation.RefreshAt, "刷新");
            yield return Metric("arknights.security", "明日方舟", "保全派驻数据增补仪", _arknightsSnapshot.SecurityService.Current, _arknightsSnapshot.SecurityService.Maximum, _arknightsSnapshot.SecurityService.RefreshAt, "刷新");
            yield return Metric("arknights.securityStrips", "明日方舟", "保全派驻数据增补条", _arknightsSnapshot.SecurityServiceStrips.Current, _arknightsSnapshot.SecurityServiceStrips.Maximum, _arknightsSnapshot.SecurityServiceStrips.RefreshAt, "刷新");
        }

        if (_endfieldSnapshot is not null)
        {
            yield return Metric("endfield.sanity", "终末地", "理智", _endfieldSnapshot.Sanity.Current, _endfieldSnapshot.Sanity.Maximum, _endfieldSnapshot.Sanity.FullAt, "回满");
            yield return Metric("endfield.dailyActivity", "终末地", "每日活跃度", _endfieldSnapshot.DailyActivity.Current, _endfieldSnapshot.DailyActivity.Maximum, null, "刷新");
            yield return Metric("endfield.weeklyTasks", "终末地", "每周事务", _endfieldSnapshot.WeeklyTasks.Current, _endfieldSnapshot.WeeklyTasks.Maximum, null, "刷新");
            yield return Metric("endfield.passLevel", "终末地", "通行证等级", _endfieldSnapshot.PassLevel.Current, _endfieldSnapshot.PassLevel.Maximum, null, "结束");
        }

        if (_wutheringWavesSnapshot is not null)
        {
            yield return Metric("wuwa.waveplates", "鸣潮", "结晶波片", _wutheringWavesSnapshot.Waveplates.Current, _wutheringWavesSnapshot.Waveplates.Maximum, _wutheringWavesSnapshot.Waveplates.RefreshAt, "回满");
            yield return Metric("wuwa.crystalSolvent", "鸣潮", "结晶单质", _wutheringWavesSnapshot.CrystalSolvent.Current, _wutheringWavesSnapshot.CrystalSolvent.Maximum, null, "刷新");
            yield return Metric("wuwa.dailyActivity", "鸣潮", "每日活跃度", _wutheringWavesSnapshot.DailyActivity.Current, _wutheringWavesSnapshot.DailyActivity.Maximum, null, "刷新");
            yield return Metric("wuwa.weeklyVoyage", "鸣潮", "周度游历", _wutheringWavesSnapshot.WeeklyVoyage.Current, _wutheringWavesSnapshot.WeeklyVoyage.Maximum, _wutheringWavesSnapshot.WeeklyVoyage.RefreshAt, "刷新");
            yield return Metric("wuwa.weeklyBoss", "鸣潮", "战歌重奏剩余收取次数", _wutheringWavesSnapshot.WeeklyBoss.Current, _wutheringWavesSnapshot.WeeklyBoss.Maximum, _wutheringWavesSnapshot.WeeklyBoss.RefreshAt, "刷新");
            yield return Metric("wuwa.battlePass", "鸣潮", "先约电台等级", _wutheringWavesSnapshot.BattlePassLevel.Current, _wutheringWavesSnapshot.BattlePassLevel.Maximum, _wutheringWavesSnapshot.BattlePassLevel.ExpireAt, "结束");
            yield return Metric("wuwa.tower", "鸣潮", "逆境深塔", 0, 0, _wutheringWavesSnapshot.TowerResetAt, "刷新");
            yield return Metric("wuwa.sea", "鸣潮", "冥歌海墟", 0, 0, _wutheringWavesSnapshot.SeaResetAt, "刷新");
            yield return Metric("wuwa.finalMatrix", "鸣潮", "终焉矩阵", 0, 0, _wutheringWavesSnapshot.FinalBattleEndAt, "结束");
        }

        if (_yihuanSnapshot is not null)
        {
            yield return Metric("yihuan.naturePixels", "异环", "本性像素", _yihuanSnapshot.NaturePixels.Current, _yihuanSnapshot.NaturePixels.Maximum, _yihuanSnapshot.NaturePixels.FullAt, "回满");
            yield return Metric("yihuan.cityVitality", "异环", "都市活力", _yihuanSnapshot.CityVitality.Current, _yihuanSnapshot.CityVitality.Maximum, _yihuanSnapshot.CityVitality.FullAt, "回满");
            yield return Metric("yihuan.dailyActivity", "异环", "活跃度", _yihuanSnapshot.DailyActivity.Current, _yihuanSnapshot.DailyActivity.Maximum, null, "刷新");
            yield return Metric("yihuan.weeklyBoss", "异环", "周本次数", _yihuanSnapshot.WeeklyBoss.Current, _yihuanSnapshot.WeeklyBoss.Maximum, null, "刷新");
        }
    }

    private static NotificationMetricState Metric(
        string id,
        string gameTitle,
        string metricTitle,
        int current,
        int maximum,
        DateTimeOffset? periodAt,
        string periodLabel)
    {
        return new NotificationMetricState(id, gameTitle, metricTitle, current, maximum, periodAt, periodLabel);
    }

    private static bool Compare(int value, string comparisonOperator, double threshold)
    {
        return comparisonOperator switch
        {
            ">" => value > threshold,
            "≤" => value <= threshold,
            "<" => value < threshold,
            _ => value >= threshold
        };
    }

    private static bool IsTimeWindow(DateTimeOffset now, int hour, int minute, TimeSpan tolerance)
    {
        var local = now.LocalDateTime;
        var target = local.Date.AddHours(hour).AddMinutes(minute);
        return local >= target && local < target.Add(tolerance);
    }

    private static DayOfWeek ToDayOfWeek(string weekday)
    {
        return weekday switch
        {
            "周二" => DayOfWeek.Tuesday,
            "周三" => DayOfWeek.Wednesday,
            "周四" => DayOfWeek.Thursday,
            "周五" => DayOfWeek.Friday,
            "周六" => DayOfWeek.Saturday,
            "周日" => DayOfWeek.Sunday,
            _ => DayOfWeek.Monday
        };
    }

    private sealed record NotificationMetricState(
        string Id,
        string GameTitle,
        string MetricTitle,
        int Current,
        int Maximum,
        DateTimeOffset? PeriodAt,
        string PeriodLabel);

    private sealed record NotificationMetricDefinition(
        string Id,
        string GameTitle,
        string MetricTitle,
        string DefaultCategory);

    private static IReadOnlyList<NotificationMetricDefinition> NotificationMetricDefinitions { get; } = new[]
    {
        new NotificationMetricDefinition("arknights.sanity", "明日方舟", "理智", "日常"),
        new NotificationMetricDefinition("arknights.drones", "明日方舟", "无人机", "日常"),
        new NotificationMetricDefinition("arknights.orders", "明日方舟", "订单进度", "日常"),
        new NotificationMetricDefinition("arknights.manufacture", "明日方舟", "制造进度", "日常"),
        new NotificationMetricDefinition("arknights.tired", "明日方舟", "干员疲劳", "日常"),
        new NotificationMetricDefinition("arknights.annihilation", "明日方舟", "每周剿灭", "周常"),
        new NotificationMetricDefinition("arknights.security", "明日方舟", "保全派驻数据增补仪", "周期"),
        new NotificationMetricDefinition("arknights.securityStrips", "明日方舟", "保全派驻数据增补条", "周期"),
        new NotificationMetricDefinition("endfield.sanity", "终末地", "理智", "日常"),
        new NotificationMetricDefinition("endfield.dailyActivity", "终末地", "每日活跃度", "日常"),
        new NotificationMetricDefinition("endfield.weeklyTasks", "终末地", "每周事务", "周常"),
        new NotificationMetricDefinition("endfield.passLevel", "终末地", "通行证等级", "周常"),
        new NotificationMetricDefinition("wuwa.waveplates", "鸣潮", "结晶波片", "日常"),
        new NotificationMetricDefinition("wuwa.crystalSolvent", "鸣潮", "结晶单质", "日常"),
        new NotificationMetricDefinition("wuwa.dailyActivity", "鸣潮", "每日活跃度", "日常"),
        new NotificationMetricDefinition("wuwa.weeklyVoyage", "鸣潮", "周度游历", "周常"),
        new NotificationMetricDefinition("wuwa.weeklyBoss", "鸣潮", "战歌重奏剩余收取次数", "周常"),
        new NotificationMetricDefinition("wuwa.battlePass", "鸣潮", "先约电台等级", "周常"),
        new NotificationMetricDefinition("wuwa.tower", "鸣潮", "逆境深塔", "周期"),
        new NotificationMetricDefinition("wuwa.sea", "鸣潮", "冥歌海墟", "周期"),
        new NotificationMetricDefinition("wuwa.finalMatrix", "鸣潮", "终焉矩阵", "周期"),
        new NotificationMetricDefinition("yihuan.naturePixels", "异环", "本性像素", "日常"),
        new NotificationMetricDefinition("yihuan.cityVitality", "异环", "都市活力", "日常"),
        new NotificationMetricDefinition("yihuan.dailyActivity", "异环", "活跃度", "日常"),
        new NotificationMetricDefinition("yihuan.weeklyBoss", "异环", "周本次数", "周常")
    };

    private static string BuildSignInSummary(IReadOnlyList<SklandSignInResult> results)
    {
        return string.Join(
            "\n",
            results.Select(result =>
            {
                var state = result.State switch
                {
                    SklandSignInState.Success => "\u6210\u529F",
                    SklandSignInState.AlreadySigned => "\u5DF2\u7B7E\u5230",
                    _ => "\u5931\u8D25"
                };
                return $"[{result.GameName}] {result.RoleName} {state}\uFF1A{result.Message}";
            }));
    }

    private double GetAutoRefreshInterval(GameDashboardKind game)
    {
        return game switch
        {
            GameDashboardKind.Arknights => _arknightsAutoRefreshIntervalMinutes,
            GameDashboardKind.Endfield => _endfieldAutoRefreshIntervalMinutes,
            GameDashboardKind.WutheringWaves => _wutheringWavesAutoRefreshIntervalMinutes,
            GameDashboardKind.Yihuan => _yihuanAutoRefreshIntervalMinutes,
            _ => 5
        };
    }

    private bool GetAutoRefreshEnabled(GameDashboardKind game)
    {
        return game switch
        {
            GameDashboardKind.Arknights => _arknightsAutoRefreshEnabled,
            GameDashboardKind.Endfield => _endfieldAutoRefreshEnabled,
            GameDashboardKind.WutheringWaves => _wutheringWavesAutoRefreshEnabled,
            GameDashboardKind.Yihuan => _yihuanAutoRefreshEnabled,
            _ => false
        };
    }

    private bool SetAutoRefreshEnabled(GameDashboardKind game, bool value)
    {
        return game switch
        {
            GameDashboardKind.Arknights => SetProperty(ref _arknightsAutoRefreshEnabled, value, nameof(AutoRefreshEnabled)),
            GameDashboardKind.Endfield => SetProperty(ref _endfieldAutoRefreshEnabled, value, nameof(AutoRefreshEnabled)),
            GameDashboardKind.WutheringWaves => SetProperty(ref _wutheringWavesAutoRefreshEnabled, value, nameof(AutoRefreshEnabled)),
            GameDashboardKind.Yihuan => SetProperty(ref _yihuanAutoRefreshEnabled, value, nameof(AutoRefreshEnabled)),
            _ => false
        };
    }

    private bool SetAutoRefreshInterval(GameDashboardKind game, double value)
    {
        value = NormalizeAutoRefreshInterval(value);
        return game switch
        {
            GameDashboardKind.Arknights => SetProperty(ref _arknightsAutoRefreshIntervalMinutes, value, nameof(AutoRefreshIntervalMinutes)),
            GameDashboardKind.Endfield => SetProperty(ref _endfieldAutoRefreshIntervalMinutes, value, nameof(AutoRefreshIntervalMinutes)),
            GameDashboardKind.WutheringWaves => SetProperty(ref _wutheringWavesAutoRefreshIntervalMinutes, value, nameof(AutoRefreshIntervalMinutes)),
            GameDashboardKind.Yihuan => SetProperty(ref _yihuanAutoRefreshIntervalMinutes, value, nameof(AutoRefreshIntervalMinutes)),
            _ => false
        };
    }

    private string GetLaunchPath(GameDashboardKind game, bool launchScript)
    {
        return game switch
        {
            GameDashboardKind.Arknights => launchScript ? _arknightsScriptPath : _arknightsGamePath,
            GameDashboardKind.Endfield => launchScript ? _endfieldScriptPath : _endfieldGamePath,
            GameDashboardKind.WutheringWaves => launchScript ? _wutheringWavesScriptPath : _wutheringWavesGamePath,
            GameDashboardKind.Yihuan => launchScript ? _yihuanScriptPath : _yihuanGamePath,
            _ => string.Empty
        };
    }

    private bool SetLaunchPath(GameDashboardKind game, bool launchScript, string? value)
    {
        value = value?.Trim() ?? string.Empty;
        return game switch
        {
            GameDashboardKind.Arknights when launchScript => SetProperty(ref _arknightsScriptPath, value, nameof(SelectedScriptLaunchPath)),
            GameDashboardKind.Arknights => SetProperty(ref _arknightsGamePath, value, nameof(SelectedGameLaunchPath)),
            GameDashboardKind.Endfield when launchScript => SetProperty(ref _endfieldScriptPath, value, nameof(SelectedScriptLaunchPath)),
            GameDashboardKind.Endfield => SetProperty(ref _endfieldGamePath, value, nameof(SelectedGameLaunchPath)),
            GameDashboardKind.WutheringWaves when launchScript => SetProperty(ref _wutheringWavesScriptPath, value, nameof(SelectedScriptLaunchPath)),
            GameDashboardKind.WutheringWaves => SetProperty(ref _wutheringWavesGamePath, value, nameof(SelectedGameLaunchPath)),
            GameDashboardKind.Yihuan when launchScript => SetProperty(ref _yihuanScriptPath, value, nameof(SelectedScriptLaunchPath)),
            GameDashboardKind.Yihuan => SetProperty(ref _yihuanGamePath, value, nameof(SelectedGameLaunchPath)),
            _ => false
        };
    }

    private void SetLaunchPathAndSave(GameDashboardKind game, bool launchScript, string? value)
    {
        if (SetLaunchPath(game, launchScript, value))
        {
            OnLaunchPathChanged();
        }
    }

    private void OnLaunchPathChanged()
    {
        OnPropertyChanged(nameof(SelectedGameLaunchPath));
        OnPropertyChanged(nameof(SelectedScriptLaunchPath));
        OnPropertyChanged(nameof(ArknightsGameLaunchPath));
        OnPropertyChanged(nameof(ArknightsScriptLaunchPath));
        OnPropertyChanged(nameof(EndfieldGameLaunchPath));
        OnPropertyChanged(nameof(EndfieldScriptLaunchPath));
        OnPropertyChanged(nameof(WutheringWavesGameLaunchPath));
        OnPropertyChanged(nameof(WutheringWavesScriptLaunchPath));
        OnPropertyChanged(nameof(YihuanGameLaunchPath));
        OnPropertyChanged(nameof(YihuanScriptLaunchPath));
        _ = SaveSettingsAsync();
    }

    private static double NormalizeAutoRefreshInterval(double value)
    {
        return Math.Clamp(double.IsNaN(value) || value <= 0 ? 5 : value, 1, 180);
    }

    private static double NormalizeNotificationCooldownMinutes(double value)
    {
        return Math.Clamp(double.IsNaN(value) || value <= 0 ? 60 : Math.Round(value), 1, 1440);
    }


    private void NotifySnapshotChanged()
    {
        OnPropertyChanged(nameof(DoctorName));
        OnPropertyChanged(nameof(SanityValue));
        OnPropertyChanged(nameof(SanityMax));
        OnPropertyChanged(nameof(SanityText));
        OnPropertyChanged(nameof(SanityRecoveryText));
        OnPropertyChanged(nameof(DroneValue));
        OnPropertyChanged(nameof(DroneMax));
        OnPropertyChanged(nameof(DroneText));
        OnPropertyChanged(nameof(DroneRecoveryText));
        OnPropertyChanged(nameof(TrainingOperatorName));
        OnPropertyChanged(nameof(TrainingSkillText));
        OnPropertyChanged(nameof(TrainingRemainingText));
        OnPropertyChanged(nameof(TrainingCompleteAtText));
        OnPropertyChanged(nameof(OrderValue));
        OnPropertyChanged(nameof(OrderMax));
        OnPropertyChanged(nameof(OrderText));
        OnPropertyChanged(nameof(OrderCompletionText));
        OnPropertyChanged(nameof(ManufactureValue));
        OnPropertyChanged(nameof(ManufactureMax));
        OnPropertyChanged(nameof(ManufactureText));
        OnPropertyChanged(nameof(ManufactureCompletionText));
        OnPropertyChanged(nameof(TiredOperatorsText));
        OnPropertyChanged(nameof(AnnihilationValue));
        OnPropertyChanged(nameof(AnnihilationMax));
        OnPropertyChanged(nameof(AnnihilationText));
        OnPropertyChanged(nameof(AnnihilationRefreshText));
        OnPropertyChanged(nameof(SecurityServiceValue));
        OnPropertyChanged(nameof(SecurityServiceMax));
        OnPropertyChanged(nameof(SecurityServiceText));
        OnPropertyChanged(nameof(SecurityServiceStripValue));
        OnPropertyChanged(nameof(SecurityServiceStripMax));
        OnPropertyChanged(nameof(SecurityServiceStripText));
        OnPropertyChanged(nameof(SecurityServiceRefreshText));
        OnPropertyChanged(nameof(EndfieldSanityValue));
        OnPropertyChanged(nameof(EndfieldSanityMax));
        OnPropertyChanged(nameof(EndfieldSanityText));
        OnPropertyChanged(nameof(EndfieldSanityRecoveryText));
        OnPropertyChanged(nameof(EndfieldDailyActivityValue));
        OnPropertyChanged(nameof(EndfieldDailyActivityMax));
        OnPropertyChanged(nameof(EndfieldDailyActivityText));
        OnPropertyChanged(nameof(EndfieldWeeklyTasksValue));
        OnPropertyChanged(nameof(EndfieldWeeklyTasksMax));
        OnPropertyChanged(nameof(EndfieldWeeklyTasksText));
        OnPropertyChanged(nameof(EndfieldPassLevelValue));
        OnPropertyChanged(nameof(EndfieldPassLevelMax));
        OnPropertyChanged(nameof(EndfieldPassLevelText));
        OnPropertyChanged(nameof(WutheringWaveplatesValue));
        OnPropertyChanged(nameof(WutheringWaveplatesMax));
        OnPropertyChanged(nameof(WutheringWaveplatesText));
        OnPropertyChanged(nameof(WutheringWaveplatesRecoveryText));
        OnPropertyChanged(nameof(WutheringCrystalSolventValue));
        OnPropertyChanged(nameof(WutheringCrystalSolventMax));
        OnPropertyChanged(nameof(WutheringCrystalSolventText));
        OnPropertyChanged(nameof(WutheringDailyActivityValue));
        OnPropertyChanged(nameof(WutheringDailyActivityMax));
        OnPropertyChanged(nameof(WutheringDailyActivityText));
        OnPropertyChanged(nameof(WutheringWeeklyVoyageValue));
        OnPropertyChanged(nameof(WutheringWeeklyVoyageMax));
        OnPropertyChanged(nameof(WutheringWeeklyVoyageText));
        OnPropertyChanged(nameof(WutheringWeeklyBossValue));
        OnPropertyChanged(nameof(WutheringWeeklyBossMax));
        OnPropertyChanged(nameof(WutheringWeeklyBossText));
        OnPropertyChanged(nameof(WutheringBattlePassValue));
        OnPropertyChanged(nameof(WutheringBattlePassMax));
        OnPropertyChanged(nameof(WutheringBattlePassText));
        OnPropertyChanged(nameof(WutheringTowerResetText));
        OnPropertyChanged(nameof(WutheringSeaResetText));
        OnPropertyChanged(nameof(WutheringFinalBattleEndText));
        OnPropertyChanged(nameof(WutheringSignInText));
        OnPropertyChanged(nameof(YihuanNaturePixelsValue));
        OnPropertyChanged(nameof(YihuanNaturePixelsMax));
        OnPropertyChanged(nameof(YihuanNaturePixelsText));
        OnPropertyChanged(nameof(YihuanCityVitalityValue));
        OnPropertyChanged(nameof(YihuanCityVitalityMax));
        OnPropertyChanged(nameof(YihuanCityVitalityText));
        OnPropertyChanged(nameof(YihuanDailyActivityValue));
        OnPropertyChanged(nameof(YihuanDailyActivityMax));
        OnPropertyChanged(nameof(YihuanDailyActivityText));
        OnPropertyChanged(nameof(YihuanWeeklyBossValue));
        OnPropertyChanged(nameof(YihuanWeeklyBossMax));
        OnPropertyChanged(nameof(YihuanWeeklyBossText));
        OnPropertyChanged(nameof(YihuanSignInText));
    }

    private void NotifyAccountChanged()
    {
        OnPropertyChanged(nameof(AccountBadgeText));
    }

    private void NotifyGameChanged()
    {
        OnPropertyChanged(nameof(IsArknightsSelected));
        OnPropertyChanged(nameof(IsEndfieldSelected));
        OnPropertyChanged(nameof(IsWutheringWavesSelected));
        OnPropertyChanged(nameof(IsYihuanSelected));
        OnPropertyChanged(nameof(GameSelectorThumbColumn));
        OnPropertyChanged(nameof(ArknightsDashboardVisibility));
        OnPropertyChanged(nameof(EndfieldDashboardVisibility));
        OnPropertyChanged(nameof(WutheringWavesDashboardVisibility));
        OnPropertyChanged(nameof(YihuanDashboardVisibility));
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(LaunchGameButtonText));
        OnPropertyChanged(nameof(LaunchScriptButtonText));
        OnPropertyChanged(nameof(SelectedGameLaunchPath));
        OnPropertyChanged(nameof(SelectedScriptLaunchPath));
        OnPropertyChanged(nameof(AccountPanelSubtitle));
        OnPropertyChanged(nameof(CredFieldHeader));
        OnPropertyChanged(nameof(TokenFieldHeader));
        OnPropertyChanged(nameof(CookieFieldHeader));
        OnPropertyChanged(nameof(UserIdFieldHeader));
        OnPropertyChanged(nameof(DeviceIdFieldHeader));
        OnPropertyChanged(nameof(AutoRefreshEnabled));
        OnPropertyChanged(nameof(AutoRefreshIntervalMinutes));
        OnPropertyChanged(nameof(AutoRefreshSummary));
        OnPropertyChanged(nameof(SignInButtonText));
        OnPropertyChanged(nameof(AccountBadgeText));
    }

    private static string FormatMeter(ResourceMeter? meter)
    {
        return meter is null ? "0/0" : $"{meter.Current}/{meter.Maximum}";
    }

    private static string FormatProgress(ProgressStatus? progress)
    {
        return progress is null ? "0/0" : $"{progress.Current}/{progress.Maximum}";
    }

    private static string FormatWuwaResource(WutheringWavesResourceStatus? status)
    {
        if (status is null)
        {
            return "0/0";
        }

        if (!string.IsNullOrWhiteSpace(status.Value))
        {
            return status.Value;
        }

        return status.Maximum > 0 ? $"{status.Current}/{status.Maximum}" : $"{status.Current}";
    }

    private static string FormatCompletion(string label, DateTimeOffset? completeAt)
    {
        if (completeAt is null)
        {
            return "\u65F6\u95F4\u672A\u77E5";
        }

        if (completeAt <= DateTimeOffset.Now)
        {
            return "\u5DF2\u6EE1";
        }

        return $"{label} {FormatClockWithDay(completeAt.Value)} \u00B7 \u8FD8\u9700 {FormatDuration(completeAt.Value - DateTimeOffset.Now)}";
    }

    private static string FormatCompleteAt(string label, DateTimeOffset? completeAt)
    {
        if (completeAt is null)
        {
            return "-";
        }

        if (completeAt <= DateTimeOffset.Now)
        {
            return "\u5DF2\u5B8C\u6210";
        }

        var timeText = FormatClockWithDay(completeAt.Value);
        return string.IsNullOrWhiteSpace(label) ? timeText : $"{label} {timeText}";
    }

    private static string FormatEndAt(string label, DateTimeOffset? endAt)
    {
        if (endAt is null)
        {
            return "\u7ED3\u675F\u65F6\u95F4\u672A\u77E5";
        }

        if (endAt <= DateTimeOffset.Now)
        {
            return "\u5DF2\u7ED3\u675F";
        }

        return $"{label} {FormatClockWithDay(endAt.Value)} \u00B7 \u8FD8\u9700 {FormatDuration(endAt.Value - DateTimeOffset.Now)}";
    }

    private static string FormatClockWithDay(DateTimeOffset time)
    {
        var localTime = time.LocalDateTime;
        var today = DateTime.Now.Date;
        if (localTime.Date == today)
        {
            return localTime.ToString("HH:mm");
        }

        if (localTime.Date == today.AddDays(1))
        {
            return $"\u6B21\u65E5 {localTime:HH:mm}";
        }

        return localTime.ToString("MM-dd HH:mm");
    }

    private static string FormatRefreshAt(DateTimeOffset? refreshAt)
    {
        if (refreshAt is null)
        {
            return "\u5237\u65B0\u65F6\u95F4\u672A\u77E5";
        }

        if (refreshAt <= DateTimeOffset.Now)
        {
            return "\u5373\u5C06\u5237\u65B0";
        }

        return $"\u5237\u65B0 {refreshAt:MM-dd HH:mm} \u00B7 \u8FD8\u9700 {FormatDuration(refreshAt.Value - DateTimeOffset.Now)}";
    }

    private static string FormatRemaining(DateTimeOffset? completeAt)
    {
        if (completeAt is null)
        {
            return "-";
        }

        var remaining = completeAt.Value - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
        {
            return "\u5DF2\u5B8C\u6210";
        }

        return FormatDuration(remaining);
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
        {
            return $"{(int)duration.TotalDays}d {duration.Hours}h";
        }

        if (duration.TotalHours >= 1)
        {
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        }

        return $"{Math.Max((int)Math.Ceiling(duration.TotalMinutes), 1)}m";
    }
}
