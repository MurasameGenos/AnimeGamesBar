using System.Collections.ObjectModel;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Kuro;
using AnimeGamesBar.App.Services.Notifications;
using AnimeGamesBar.App.Services.Arknights;
using AnimeGamesBar.App.Services.Settings;
using AnimeGamesBar.App.Services.Skland;
using AnimeGamesBar.App.Services.Startup;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

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
    private readonly ISettingsStore _settingsStore;
    private readonly IAppNotificationService _notificationService;
    private readonly IStartupService _startupService;
    private readonly List<ArknightsPlayerBinding> _arknightsBindings = new();
    private readonly List<ArknightsPlayerBinding> _endfieldBindings = new();
    private readonly List<ArknightsPlayerBinding> _wutheringWavesBindings = new();

    private SklandCredential _arknightsCredential = SklandCredential.Empty;
    private SklandCredential _endfieldCredential = SklandCredential.Empty;
    private SklandCredential _wutheringWavesCredential = SklandCredential.Empty;
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
    private ArknightsAccountStatus? _arknightsSnapshot;
    private EndfieldAccountStatus? _endfieldSnapshot;
    private WutheringWavesAccountStatus? _wutheringWavesSnapshot;
    private GameDashboardKind _selectedGame = GameDashboardKind.Arknights;
    private bool _autoRefreshEnabled;
    private double _arknightsAutoRefreshIntervalMinutes = 5;
    private double _endfieldAutoRefreshIntervalMinutes = 5;
    private double _wutheringWavesAutoRefreshIntervalMinutes = 5;
    private bool _isSettingsPageOpen;
    private bool _useDarkTheme = true;
    private bool _autoSignEnabled = true;
    private bool _notificationsEnabled = true;
    private bool _startWithWindows;
    private bool _settingsLoaded;
    private bool _isApplyingSettings;

    public MainViewModel(
        ICredentialStore credentialStore,
        IArknightsMonitor monitor,
        ISklandLoginService loginService,
        ISklandSignInService signInService,
        IKuroMonitor kuroMonitor,
        IKuroSignInService kuroSignInService,
        IKuroLoginService kuroLoginService,
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
        _settingsStore = settingsStore;
        _notificationService = notificationService;
        _startupService = startupService;

        RefreshCommand = new AsyncCommand(RefreshAsync);
        RefreshAllCommand = new AsyncCommand(RefreshAllAsync);
        RefreshArknightsCommand = new AsyncCommand(cancellationToken => RefreshAutoGameAsync(GameDashboardKind.Arknights, cancellationToken));
        RefreshEndfieldCommand = new AsyncCommand(cancellationToken => RefreshAutoGameAsync(GameDashboardKind.Endfield, cancellationToken));
        RefreshWutheringWavesCommand = new AsyncCommand(cancellationToken => RefreshAutoGameAsync(GameDashboardKind.WutheringWaves, cancellationToken));
        SignInCommand = new AsyncCommand(SignInAllAsync);
        SaveCredentialCommand = new AsyncCommand(SaveCredentialAsync);
        ClearCredentialCommand = new AsyncCommand(ClearCredentialAsync);
        StartLoginCommand = new AsyncCommand(StartLoginAsync);
        OpenSettingsCommand = new AsyncCommand(_ =>
        {
            IsSettingsPageOpen = true;
            return Task.CompletedTask;
        });
        CloseSettingsCommand = new AsyncCommand(_ =>
        {
            IsSettingsPageOpen = false;
            return Task.CompletedTask;
        });
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

        _ = InitializeAsync();
    }

    public ObservableCollection<ArknightsPlayerBinding> PlayerBindings { get; } = new();

    public Window? OwnerWindow { get; set; }

    public event EventHandler? CredentialApplied;

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand RefreshAllCommand { get; }

    public AsyncCommand RefreshArknightsCommand { get; }

    public AsyncCommand RefreshEndfieldCommand { get; }

    public AsyncCommand RefreshWutheringWavesCommand { get; }

    public AsyncCommand SignInCommand { get; }

    public AsyncCommand SaveCredentialCommand { get; }

    public AsyncCommand ClearCredentialCommand { get; }

    public AsyncCommand StartLoginCommand { get; }

    public AsyncCommand SelectArknightsCommand { get; }

    public AsyncCommand SelectEndfieldCommand { get; }

    public AsyncCommand SelectWutheringWavesCommand { get; }

    public AsyncCommand OpenSettingsCommand { get; }

    public AsyncCommand CloseSettingsCommand { get; }

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
        get => _autoRefreshEnabled;
        set
        {
            if (SetProperty(ref _autoRefreshEnabled, value))
            {
                OnPropertyChanged(nameof(AutoRefreshSummary));
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
                _ = SaveSettingsAsync();
            }
        }
    }

    public double ArknightsAutoRefreshIntervalMinutes => _arknightsAutoRefreshIntervalMinutes;

    public double EndfieldAutoRefreshIntervalMinutes => _endfieldAutoRefreshIntervalMinutes;

    public double WutheringWavesAutoRefreshIntervalMinutes => _wutheringWavesAutoRefreshIntervalMinutes;

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
            }
        }
    }

    public Visibility DashboardVisibility => IsSettingsPageOpen ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SettingsVisibility => IsSettingsPageOpen ? Visibility.Visible : Visibility.Collapsed;

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

    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set
        {
            if (SetProperty(ref _notificationsEnabled, value))
            {
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

    public int GameSelectorThumbColumn => _selectedGame switch
    {
        GameDashboardKind.Arknights => 0,
        GameDashboardKind.Endfield => 1,
        GameDashboardKind.WutheringWaves => 2,
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

    public string SelectedGameTitle => GameTitle(_selectedGame);

    public string AccountPanelSubtitle => _selectedGame switch
    {
        GameDashboardKind.Arknights => "\u7F57\u5FB7\u5C9B\u8D26\u53F7\u4E0E\u5237\u65B0\u8BBE\u7F6E",
        GameDashboardKind.Endfield => "\u7EC8\u672B\u5730\u8D26\u53F7\u4E0E\u5237\u65B0\u8BBE\u7F6E",
        GameDashboardKind.WutheringWaves => "\u5E93\u8857\u533A Token \u4E0E\u5237\u65B0\u8BBE\u7F6E",
        _ => string.Empty
    };

    public string CredFieldHeader => _selectedGame == GameDashboardKind.WutheringWaves ? "\u5907\u6CE8\uFF08\u53EF\u7559\u7A7A\uFF09" : "Cred";

    public string TokenFieldHeader => _selectedGame == GameDashboardKind.WutheringWaves ? "\u5E93\u8857\u533A Token" : "Token";

    public string CookieFieldHeader => _selectedGame == GameDashboardKind.WutheringWaves ? "\u5907\u7528 Cookie\uFF08\u53EF\u7559\u7A7A\uFF09" : "Cookie";

    public string UserIdFieldHeader => _selectedGame == GameDashboardKind.WutheringWaves ? "\u5E93\u8857\u533A User ID\uFF08\u81EA\u52A8\u586B\u5145\uFF09" : "User ID";

    public string DeviceIdFieldHeader => _selectedGame == GameDashboardKind.WutheringWaves ? "\u8BBE\u5907 devCode" : "Device ID";

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

    public string WutheringFinalBattleEndText => FormatCompleteAt("\u7ED3\u675F", _wutheringWavesSnapshot?.FinalBattleEndAt);

    public string WutheringSignInText => _wutheringWavesSnapshot?.HasSignedIn == true ? "\u4ECA\u65E5\u5DF2\u7B7E\u5230" : "\u4ECA\u65E5\u672A\u7B7E\u5230";

    private async Task InitializeAsync()
    {
        await LoadSettingsAsync(CancellationToken.None);

        var legacyCredential = await _credentialStore.LoadAsync(CancellationToken.None);
        var arknightsCredential = await _credentialStore.LoadAsync(CredentialScopeFor(GameDashboardKind.Arknights), CancellationToken.None);
        var endfieldCredential = await _credentialStore.LoadAsync(CredentialScopeFor(GameDashboardKind.Endfield), CancellationToken.None);
        var wutheringWavesCredential = await _credentialStore.LoadAsync(CredentialScopeFor(GameDashboardKind.WutheringWaves), CancellationToken.None);

        _arknightsCredential = arknightsCredential ?? legacyCredential ?? SklandCredential.Empty;
        _endfieldCredential = endfieldCredential ?? legacyCredential ?? SklandCredential.Empty;
        _wutheringWavesCredential = wutheringWavesCredential ?? SklandCredential.Empty;

        if (!_arknightsCredential.HasAnySecret && !_endfieldCredential.HasAnySecret && !_wutheringWavesCredential.HasAnySecret)
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
            NotificationsEnabled = settings.NotificationsEnabled;
            StartWithWindows = settings.StartWithWindows || _startupService.IsEnabled();
            _arknightsAutoRefreshIntervalMinutes = NormalizeAutoRefreshInterval(settings.ArknightsAutoRefreshIntervalMinutes);
            _endfieldAutoRefreshIntervalMinutes = NormalizeAutoRefreshInterval(settings.EndfieldAutoRefreshIntervalMinutes);
            _wutheringWavesAutoRefreshIntervalMinutes = NormalizeAutoRefreshInterval(settings.WutheringWavesAutoRefreshIntervalMinutes);
            OnPropertyChanged(nameof(AutoRefreshIntervalMinutes));
            OnPropertyChanged(nameof(AutoRefreshSummary));
            OnPropertyChanged(nameof(ArknightsAutoRefreshIntervalMinutes));
            OnPropertyChanged(nameof(EndfieldAutoRefreshIntervalMinutes));
            OnPropertyChanged(nameof(WutheringWavesAutoRefreshIntervalMinutes));
        }
        finally
        {
            _isApplyingSettings = false;
            _settingsLoaded = true;
        }
    }

    private async Task SaveSettingsAsync()
    {
        if (!_settingsLoaded || _isApplyingSettings)
        {
            return;
        }

        try
        {
            await _settingsStore.SaveAsync(
                new AppSettings(
                    UseDarkTheme,
                    AutoSignEnabled,
                    NotificationsEnabled,
                    StartWithWindows,
                    _arknightsAutoRefreshIntervalMinutes,
                    _endfieldAutoRefreshIntervalMinutes,
                    _wutheringWavesAutoRefreshIntervalMinutes),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            SetStatus($"\u8BBE\u7F6E\u4FDD\u5B58\u5931\u8D25\uFF1A{ex.Message}", InfoBarSeverity.Warning);
        }
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
            if (!_arknightsCredential.HasAnySecret && !_endfieldCredential.HasAnySecret && !_wutheringWavesCredential.HasAnySecret)
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

    private async Task SignInAllAsync(CancellationToken cancellationToken, bool showNotification)
    {
        try
        {
            CommitCurrentCredentialFields();
            if (!_arknightsCredential.HasAnySecret && !_endfieldCredential.HasAnySecret && !_wutheringWavesCredential.HasAnySecret)
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

            if (results.Count == 0)
            {
                SetStatus("\u6CA1\u6709\u627E\u5230\u53EF\u7B7E\u5230\u7684\u7ED1\u5B9A\u89D2\u8272\u3002", InfoBarSeverity.Warning);
                return;
            }

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
        catch (Exception ex)
        {
            var message = $"\u7B7E\u5230\u5931\u8D25\uFF1A{ex.Message}";
            SetStatus(message, InfoBarSeverity.Error);
            if (showNotification)
            {
                await _notificationService.ShowAsync("\u6E38\u620F\u7B7E\u5230\u5931\u8D25", message, cancellationToken);
            }
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
        else
        {
            _wutheringWavesSnapshot = await _kuroMonitor.GetStatusAsync(credential, selectedBinding, cancellationToken);
            updatedAt = _wutheringWavesSnapshot.UpdatedAt;
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
        else
        {
            _wutheringWavesSnapshot = null;
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
    }

    private static string GameTitle(GameDashboardKind game)
    {
        return game switch
        {
            GameDashboardKind.Arknights => "\u660E\u65E5\u65B9\u821F",
            GameDashboardKind.Endfield => "\u7EC8\u672B\u5730",
            GameDashboardKind.WutheringWaves => "\u9E23\u6F6E",
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
            _ => "\u672A\u767B\u5F55"
        };
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusSeverity = severity;
        StatusMessage = message;
    }

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
            _ => 5
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
            _ => false
        };
    }

    private static double NormalizeAutoRefreshInterval(double value)
    {
        return Math.Clamp(double.IsNaN(value) || value <= 0 ? 5 : value, 1, 180);
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
        OnPropertyChanged(nameof(GameSelectorThumbColumn));
        OnPropertyChanged(nameof(ArknightsDashboardVisibility));
        OnPropertyChanged(nameof(EndfieldDashboardVisibility));
        OnPropertyChanged(nameof(WutheringWavesDashboardVisibility));
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(AccountPanelSubtitle));
        OnPropertyChanged(nameof(CredFieldHeader));
        OnPropertyChanged(nameof(TokenFieldHeader));
        OnPropertyChanged(nameof(CookieFieldHeader));
        OnPropertyChanged(nameof(UserIdFieldHeader));
        OnPropertyChanged(nameof(DeviceIdFieldHeader));
        OnPropertyChanged(nameof(AutoRefreshIntervalMinutes));
        OnPropertyChanged(nameof(AutoRefreshSummary));
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
