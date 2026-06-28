using System.Collections.ObjectModel;
using AnimeGamesBar.App.Models;
using AnimeGamesBar.App.Services.Arknights;
using AnimeGamesBar.App.Services.Skland;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace AnimeGamesBar.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ICredentialStore _credentialStore;
    private readonly IArknightsMonitor _monitor;
    private readonly ISklandLoginService _loginService;

    private string _cred = string.Empty;
    private string _token = string.Empty;
    private string _cookie = string.Empty;
    private string _userId = string.Empty;
    private string _deviceId = Guid.NewGuid().ToString("N");
    private string _doctorName = "\u672A\u767B\u5F55";
    private string _statusMessage = string.Empty;
    private InfoBarSeverity _statusSeverity = InfoBarSeverity.Informational;
    private ArknightsPlayerBinding? _selectedPlayerBinding;
    private ArknightsAccountStatus? _snapshot;
    private bool _autoRefreshEnabled;

    public MainViewModel(
        ICredentialStore credentialStore,
        IArknightsMonitor monitor,
        ISklandLoginService loginService)
    {
        _credentialStore = credentialStore;
        _monitor = monitor;
        _loginService = loginService;

        RefreshCommand = new AsyncCommand(RefreshAsync);
        SaveCredentialCommand = new AsyncCommand(SaveCredentialAsync);
        ClearCredentialCommand = new AsyncCommand(ClearCredentialAsync);
        StartLoginCommand = new AsyncCommand(StartLoginAsync);

        _ = InitializeAsync();
    }

    public ObservableCollection<ArknightsPlayerBinding> PlayerBindings { get; } = new();

    public Window? OwnerWindow { get; set; }

    public event EventHandler? CredentialApplied;

    public AsyncCommand RefreshCommand { get; }

    public AsyncCommand SaveCredentialCommand { get; }

    public AsyncCommand ClearCredentialCommand { get; }

    public AsyncCommand StartLoginCommand { get; }

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
        get => _selectedPlayerBinding;
        set
        {
            if (SetProperty(ref _selectedPlayerBinding, value))
            {
                NotifyAccountChanged();
            }
        }
    }

    public bool AutoRefreshEnabled
    {
        get => _autoRefreshEnabled;
        set => SetProperty(ref _autoRefreshEnabled, value);
    }

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

    public int SanityValue => _snapshot?.Sanity.Current ?? 0;

    public int SanityMax => Math.Max(_snapshot?.Sanity.Maximum ?? 1, 1);

    public string SanityText => FormatMeter(_snapshot?.Sanity);

    public string SanityRecoveryText => FormatCompletion("\u56DE\u6EE1", _snapshot?.Sanity.FullAt);

    public int DroneValue => _snapshot?.Drones.Current ?? 0;

    public int DroneMax => Math.Max(_snapshot?.Drones.Maximum ?? 1, 1);

    public string DroneText => FormatMeter(_snapshot?.Drones);

    public string DroneRecoveryText => FormatCompletion("\u56DE\u6EE1", _snapshot?.Drones.FullAt);

    public string TrainingOperatorName => _snapshot?.TrainingRoom.OperatorName ?? "\u7A7A\u95F2";

    public string TrainingSkillText
    {
        get
        {
            var training = _snapshot?.TrainingRoom;
            if (training is null || !training.IsTraining)
            {
                return "-";
            }

            return training.TargetSkillLevel is null
                ? training.SkillName
                : $"{training.SkillName} -> {training.TargetSkillLevel}";
        }
    }

    public string TrainingRemainingText => FormatRemaining(_snapshot?.TrainingRoom.CompleteAt);

    public string TrainingCompleteAtText => FormatCompleteAt("\u5B8C\u6210", _snapshot?.TrainingRoom.CompleteAt);

    public int OrderValue => _snapshot?.Building.Orders.Current ?? 0;

    public int OrderMax => Math.Max(_snapshot?.Building.Orders.Maximum ?? 1, 1);

    public string OrderText => FormatProgress(_snapshot?.Building.Orders);

    public string OrderCompletionText => FormatCompleteAt("\u4E0B\u4E00\u5355", _snapshot?.Building.Orders.CompleteAt);

    public int ManufactureValue => _snapshot?.Building.Manufacture.Current ?? 0;

    public int ManufactureMax => Math.Max(_snapshot?.Building.Manufacture.Maximum ?? 1, 1);

    public string ManufactureText => FormatProgress(_snapshot?.Building.Manufacture);

    public string ManufactureCompletionText => FormatCompleteAt("\u4E0B\u4E00\u4EF6", _snapshot?.Building.Manufacture.CompleteAt);

    public string TiredOperatorsText => $"{_snapshot?.Building.TiredOperators ?? 0}";

    public int AnnihilationValue => _snapshot?.Annihilation.Current ?? 0;

    public int AnnihilationMax => _snapshot?.Annihilation.Maximum ?? 1800;

    public string AnnihilationText => $"{AnnihilationValue}/{AnnihilationMax}";

    public string AnnihilationRefreshText => FormatRefreshAt(_snapshot?.Annihilation.RefreshAt);

    public int SecurityServiceValue => _snapshot?.SecurityService.Current ?? 0;

    public int SecurityServiceMax => _snapshot?.SecurityService.Maximum ?? 24;

    public string SecurityServiceText => $"{SecurityServiceValue}/{SecurityServiceMax}";

    public int SecurityServiceStripValue => _snapshot?.SecurityServiceStrips.Current ?? 0;

    public int SecurityServiceStripMax => _snapshot?.SecurityServiceStrips.Maximum ?? 60;

    public string SecurityServiceStripText => $"{SecurityServiceStripValue}/{SecurityServiceStripMax}";

    public string SecurityServiceRefreshText => FormatRefreshAt(
        _snapshot?.SecurityService.RefreshAt ?? _snapshot?.SecurityServiceStrips.RefreshAt);

    private async Task InitializeAsync()
    {
        var credential = await _credentialStore.LoadAsync(CancellationToken.None);
        if (credential is null)
        {
            SetStatus("\u672A\u627E\u5230\u672C\u5730\u51ED\u636E\u3002", InfoBarSeverity.Informational);
            return;
        }

        ApplyCredential(credential);
        SetStatus("\u5DF2\u52A0\u8F7D\u672C\u5730\u51ED\u636E\u3002", InfoBarSeverity.Success);
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var credential = BuildCredential();
            if (!credential.HasAnySecret)
            {
                SetStatus("\u7F3A\u5C11\u8D26\u53F7\u51ED\u636E\u3002", InfoBarSeverity.Warning);
                return;
            }

            if (PlayerBindings.Count == 0)
            {
                var bindingResult = await _monitor.GetBindingsAsync(credential, cancellationToken);
                if (!string.IsNullOrWhiteSpace(bindingResult.ResolvedUserId) &&
                    !string.Equals(UserId, bindingResult.ResolvedUserId, StringComparison.Ordinal))
                {
                    UserId = bindingResult.ResolvedUserId;
                    credential = BuildCredential();
                    await _credentialStore.SaveAsync(credential, cancellationToken);
                }

                PlayerBindings.Clear();
                foreach (var binding in bindingResult.Bindings)
                {
                    PlayerBindings.Add(binding);
                }

                SelectedPlayerBinding ??= PlayerBindings.FirstOrDefault();
            }

            if (SelectedPlayerBinding is null)
            {
                SetStatus("\u6CA1\u6709\u627E\u5230\u5DF2\u7ED1\u5B9A\u7684\u660E\u65E5\u65B9\u821F\u8D26\u53F7\u3002", InfoBarSeverity.Warning);
                return;
            }

            _snapshot = await _monitor.GetStatusAsync(credential, SelectedPlayerBinding, cancellationToken);
            DoctorName = _snapshot.DoctorName;
            NotifySnapshotChanged();
            SetStatus($"\u5DF2\u5237\u65B0\uFF1A{_snapshot.UpdatedAt:HH:mm:ss}", InfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, InfoBarSeverity.Error);
        }
    }

    private async Task SaveCredentialAsync(CancellationToken cancellationToken)
    {
        await _credentialStore.SaveAsync(BuildCredential(), cancellationToken);
        SetStatus("\u51ED\u636E\u5DF2\u4FDD\u5B58\u3002", InfoBarSeverity.Success);
    }

    private async Task ClearCredentialAsync(CancellationToken cancellationToken)
    {
        await _credentialStore.ClearAsync(cancellationToken);
        ApplyCredential(SklandCredential.Empty);
        PlayerBindings.Clear();
        SelectedPlayerBinding = null;
        _snapshot = null;
        DoctorName = "\u672A\u767B\u5F55";
        NotifySnapshotChanged();
        SetStatus("\u672C\u5730\u51ED\u636E\u5DF2\u6E05\u9664\u3002", InfoBarSeverity.Success);
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

            var credential = await _loginService.LoginAsync(OwnerWindow, BuildCredential(), cancellationToken);
            if (credential is null)
            {
                SetStatus("\u5DF2\u53D6\u6D88\u767B\u5F55\u3002", InfoBarSeverity.Informational);
                return;
            }

            ApplyCredential(credential);
            await _credentialStore.SaveAsync(credential, cancellationToken);
            PlayerBindings.Clear();
            SelectedPlayerBinding = null;
            _snapshot = null;
            NotifySnapshotChanged();
            SetStatus("\u767B\u5F55\u51ED\u636E\u5DF2\u4FDD\u5B58\uFF0C\u6B63\u5728\u9A8C\u8BC1\u6570\u636E\u6293\u53D6\u3002", InfoBarSeverity.Success);
            await RefreshAsync(cancellationToken);
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

    private void SetStatus(string message, InfoBarSeverity severity)
    {
        StatusSeverity = severity;
        StatusMessage = message;
    }

    private void NotifySnapshotChanged()
    {
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
    }

    private void NotifyAccountChanged()
    {
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
