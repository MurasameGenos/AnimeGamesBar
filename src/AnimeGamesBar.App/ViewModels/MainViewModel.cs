using System.Collections.ObjectModel;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Arknights;
using AnimeGamesBar.App.Services.Skland;
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
    private readonly List<ArknightsPlayerBinding> _arknightsBindings = new();
    private readonly List<ArknightsPlayerBinding> _endfieldBindings = new();

    private SklandCredential _arknightsCredential = SklandCredential.Empty;
    private SklandCredential _endfieldCredential = SklandCredential.Empty;
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
    private ArknightsAccountStatus? _arknightsSnapshot;
    private EndfieldAccountStatus? _endfieldSnapshot;
    private GameDashboardKind _selectedGame = GameDashboardKind.Arknights;
    private bool _autoRefreshEnabled;
    private double _autoRefreshIntervalMinutes = 5;

    public MainViewModel(
        ICredentialStore credentialStore,
        IArknightsMonitor monitor,
        ISklandLoginService loginService)
    {
        _credentialStore = credentialStore;
        _monitor = monitor;
        _loginService = loginService;

        RefreshCommand = new AsyncCommand(RefreshAsync);
        RefreshAllCommand = new AsyncCommand(RefreshAllAsync);
        SaveCredentialCommand = new AsyncCommand(SaveCredentialAsync);
        ClearCredentialCommand = new AsyncCommand(ClearCredentialAsync);
        StartLoginCommand = new AsyncCommand(StartLoginAsync);
        SelectArknightsCommand = new AsyncCommand(_ =>
        {
            SelectGame(GameDashboardKind.Arknights);
            return Task.CompletedTask;
        });
        SelectEndfieldCommand = new AsyncCommand(_ =>
        {
            SelectGame(GameDashboardKind.Endfield);
            return Task.CompletedTask;
        });

        _ = InitializeAsync();
    }

    public ObservableCollection<ArknightsPlayerBinding> PlayerBindings { get; } = new();

    public Window? OwnerWindow { get; set; }

    public event EventHandler? CredentialApplied;

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand RefreshAllCommand { get; }

    public AsyncCommand SaveCredentialCommand { get; }

    public AsyncCommand ClearCredentialCommand { get; }

    public AsyncCommand StartLoginCommand { get; }

    public AsyncCommand SelectArknightsCommand { get; }

    public AsyncCommand SelectEndfieldCommand { get; }

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
            var changed = _selectedGame == GameDashboardKind.Arknights
                ? SetProperty(ref _selectedArknightsBinding, value)
                : SetProperty(ref _selectedEndfieldBinding, value);
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
        get => _autoRefreshIntervalMinutes;
        set
        {
            var interval = Math.Clamp(double.IsNaN(value) ? 5 : value, 1, 180);
            if (SetProperty(ref _autoRefreshIntervalMinutes, interval))
            {
                OnPropertyChanged(nameof(AutoRefreshSummary));
            }
        }
    }

    public string AutoRefreshSummary => AutoRefreshEnabled
        ? $"\u6BCF {AutoRefreshIntervalMinutes:0} \u5206\u949F\u5237\u65B0"
        : "\u81EA\u52A8\u5237\u65B0\u5DF2\u5173\u95ED";

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

    public int GameSelectorThumbColumn => _selectedGame == GameDashboardKind.Arknights ? 0 : 1;

    public Visibility ArknightsDashboardVisibility => _selectedGame == GameDashboardKind.Arknights
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility EndfieldDashboardVisibility => _selectedGame == GameDashboardKind.Endfield
        ? Visibility.Visible
        : Visibility.Collapsed;

    public string SelectedGameTitle => _selectedGame == GameDashboardKind.Arknights
        ? "\u660E\u65E5\u65B9\u821F"
        : "\u660E\u65E5\u65B9\u821F\uFF1A\u7EC8\u672B\u5730";

    public string AccountPanelSubtitle => _selectedGame == GameDashboardKind.Arknights
        ? "\u7F57\u5FB7\u5C9B\u8D26\u53F7\u4E0E\u5237\u65B0\u8BBE\u7F6E"
        : "\u7EC8\u672B\u5730\u8D26\u53F7\u4E0E\u5237\u65B0\u8BBE\u7F6E";

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

    public string TrainingCompleteAtText => FormatCompleteAt("\u5B8C\u6210", _arknightsSnapshot?.TrainingRoom.CompleteAt);

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

    private async Task InitializeAsync()
    {
        var legacyCredential = await _credentialStore.LoadAsync(CancellationToken.None);
        var arknightsCredential = await _credentialStore.LoadAsync(CredentialScopeFor(GameDashboardKind.Arknights), CancellationToken.None);
        var endfieldCredential = await _credentialStore.LoadAsync(CredentialScopeFor(GameDashboardKind.Endfield), CancellationToken.None);

        _arknightsCredential = arknightsCredential ?? legacyCredential ?? SklandCredential.Empty;
        _endfieldCredential = endfieldCredential ?? legacyCredential ?? SklandCredential.Empty;

        if (!_arknightsCredential.HasAnySecret && !_endfieldCredential.HasAnySecret)
        {
            SetStatus("\u672A\u627E\u5230\u672C\u5730\u51ED\u636E\u3002", InfoBarSeverity.Informational);
            return;
        }

        ApplyCredential(GetCredentialFor(_selectedGame));
        SetStatus("\u5DF2\u52A0\u8F7D\u672C\u5730\u51ED\u636E\u3002", InfoBarSeverity.Success);

        await RefreshAllAsync(CancellationToken.None);
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
            if (!_arknightsCredential.HasAnySecret && !_endfieldCredential.HasAnySecret)
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
        else
        {
            _endfieldSnapshot = await _monitor.GetEndfieldStatusAsync(credential, selectedBinding, cancellationToken);
            updatedAt = _endfieldSnapshot.UpdatedAt;
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
        else
        {
            _endfieldSnapshot = null;
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
                else
                {
                    _endfieldSnapshot = null;
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
        return game == GameDashboardKind.Arknights ? "arknights" : "endfield";
    }

    private SklandCredential GetCredentialFor(GameDashboardKind game)
    {
        return game == GameDashboardKind.Arknights ? _arknightsCredential : _endfieldCredential;
    }

    private void SetCredentialFor(GameDashboardKind game, SklandCredential credential)
    {
        if (game == GameDashboardKind.Arknights)
        {
            _arknightsCredential = credential;
        }
        else
        {
            _endfieldCredential = credential;
        }
    }

    private static string AppCodeFor(GameDashboardKind game)
    {
        return game == GameDashboardKind.Arknights ? ArknightsAppCode : EndfieldAppCode;
    }

    private List<ArknightsPlayerBinding> BindingCache(GameDashboardKind game)
    {
        return game == GameDashboardKind.Arknights ? _arknightsBindings : _endfieldBindings;
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
        return game == GameDashboardKind.Arknights ? _selectedArknightsBinding : _selectedEndfieldBinding;
    }

    private void SetSelectedBinding(GameDashboardKind game, ArknightsPlayerBinding? binding)
    {
        if (game == GameDashboardKind.Arknights)
        {
            _selectedArknightsBinding = binding;
        }
        else
        {
            _selectedEndfieldBinding = binding;
        }
    }

    private static string GameTitle(GameDashboardKind game)
    {
        return game == GameDashboardKind.Arknights
            ? "\u660E\u65E5\u65B9\u821F"
            : "\u660E\u65E5\u65B9\u821F\uFF1A\u7EC8\u672B\u5730";
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
            _ => "\u672A\u767B\u5F55"
        };
    }

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusSeverity = severity;
        StatusMessage = message;
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
    }

    private void NotifyAccountChanged()
    {
        OnPropertyChanged(nameof(AccountBadgeText));
    }

    private void NotifyGameChanged()
    {
        OnPropertyChanged(nameof(IsArknightsSelected));
        OnPropertyChanged(nameof(IsEndfieldSelected));
        OnPropertyChanged(nameof(GameSelectorThumbColumn));
        OnPropertyChanged(nameof(ArknightsDashboardVisibility));
        OnPropertyChanged(nameof(EndfieldDashboardVisibility));
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(AccountPanelSubtitle));
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

        return $"{label} {completeAt:HH:mm} \u00B7 \u8FD8\u9700 {FormatDuration(completeAt.Value - DateTimeOffset.Now)}";
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

        return $"{label} {completeAt:HH:mm}";
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
