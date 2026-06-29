using System.ComponentModel;
using AnimeGamesBar.App.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;

namespace AnimeGamesBar.App;

public sealed partial class MainWindow : Window
{
    private readonly DispatcherTimer _arknightsAutoRefreshTimer = new();
    private readonly DispatcherTimer _endfieldAutoRefreshTimer = new();
    private readonly DispatcherTimer _wutheringWavesAutoRefreshTimer = new();
    private readonly DispatcherTimer _yihuanAutoRefreshTimer = new();
    private readonly DispatcherTimer _autoSignTimer = new();
    private readonly DispatcherTimer _notificationRuleTimer = new();

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        ViewModel.OwnerWindow = this;
        ViewModel.CredentialApplied += ViewModel_OnCredentialApplied;
        ViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _arknightsAutoRefreshTimer.Tick += ArknightsAutoRefreshTimer_OnTick;
        _endfieldAutoRefreshTimer.Tick += EndfieldAutoRefreshTimer_OnTick;
        _wutheringWavesAutoRefreshTimer.Tick += WutheringWavesAutoRefreshTimer_OnTick;
        _yihuanAutoRefreshTimer.Tick += YihuanAutoRefreshTimer_OnTick;
        _autoSignTimer.Tick += AutoSignTimer_OnTick;
        _notificationRuleTimer.Tick += NotificationRuleTimer_OnTick;
        Root.DataContext = ViewModel;
        Closed += MainWindow_OnClosed;
        SetDefaultSize();
        ApplyThemePalette();
        UpdateAutoRefreshTimer();
        UpdateAutoSignTimer();
        UpdateNotificationRuleTimer();
    }

    public MainViewModel ViewModel { get; }

    private void ViewModel_OnCredentialApplied(object? sender, EventArgs e)
    {
        if (TokenBox.Password != ViewModel.Token)
        {
            TokenBox.Password = ViewModel.Token;
        }

        if (CookieBox.Password != ViewModel.Cookie)
        {
            CookieBox.Password = ViewModel.Cookie;
        }
    }

    private void TokenBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Token = TokenBox.Password;
    }

    private void CookieBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Cookie = CookieBox.Password;
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.AutoRefreshEnabled)
            or nameof(MainViewModel.ArknightsAutoRefreshEnabled)
            or nameof(MainViewModel.EndfieldAutoRefreshEnabled)
            or nameof(MainViewModel.WutheringWavesAutoRefreshEnabled)
            or nameof(MainViewModel.YihuanAutoRefreshEnabled)
            or nameof(MainViewModel.AutoRefreshIntervalMinutes)
            or nameof(MainViewModel.ArknightsAutoRefreshIntervalMinutes)
            or nameof(MainViewModel.EndfieldAutoRefreshIntervalMinutes)
            or nameof(MainViewModel.WutheringWavesAutoRefreshIntervalMinutes)
            or nameof(MainViewModel.YihuanAutoRefreshIntervalMinutes))
        {
            UpdateAutoRefreshTimer();
        }

        if (e.PropertyName == nameof(MainViewModel.DailyAutoSignEnabled))
        {
            UpdateAutoSignTimer();
        }

        if (e.PropertyName == nameof(MainViewModel.RootTheme))
        {
            ApplyThemePalette();
        }
    }

    private void ArknightsAutoRefreshTimer_OnTick(object? sender, object e)
    {
        if (ViewModel.ArknightsAutoRefreshEnabled && ViewModel.RefreshArknightsCommand.CanExecute(null))
        {
            ViewModel.RefreshArknightsCommand.Execute(null);
        }
    }

    private void EndfieldAutoRefreshTimer_OnTick(object? sender, object e)
    {
        if (ViewModel.EndfieldAutoRefreshEnabled && ViewModel.RefreshEndfieldCommand.CanExecute(null))
        {
            ViewModel.RefreshEndfieldCommand.Execute(null);
        }
    }

    private void WutheringWavesAutoRefreshTimer_OnTick(object? sender, object e)
    {
        if (ViewModel.WutheringWavesAutoRefreshEnabled && ViewModel.RefreshWutheringWavesCommand.CanExecute(null))
        {
            ViewModel.RefreshWutheringWavesCommand.Execute(null);
        }
    }

    private void YihuanAutoRefreshTimer_OnTick(object? sender, object e)
    {
        if (ViewModel.YihuanAutoRefreshEnabled && ViewModel.RefreshYihuanCommand.CanExecute(null))
        {
            ViewModel.RefreshYihuanCommand.Execute(null);
        }
    }

    private void AutoSignTimer_OnTick(object? sender, object e)
    {
        if (ViewModel.TryReserveDailyAutoSign(DateTime.Now) &&
            ViewModel.ScheduledSignInCommand.CanExecute(null))
        {
            ViewModel.ScheduledSignInCommand.Execute(null);
        }
    }

    private void NotificationRuleTimer_OnTick(object? sender, object e)
    {
        if (ViewModel.EvaluateNotificationRulesCommand.CanExecute(null))
        {
            ViewModel.EvaluateNotificationRulesCommand.Execute(null);
        }
    }

    private void UpdateAutoRefreshTimer()
    {
        ConfigureAutoRefreshTimer(_arknightsAutoRefreshTimer, ViewModel.ArknightsAutoRefreshIntervalMinutes, ViewModel.ArknightsAutoRefreshEnabled);
        ConfigureAutoRefreshTimer(_endfieldAutoRefreshTimer, ViewModel.EndfieldAutoRefreshIntervalMinutes, ViewModel.EndfieldAutoRefreshEnabled);
        ConfigureAutoRefreshTimer(_wutheringWavesAutoRefreshTimer, ViewModel.WutheringWavesAutoRefreshIntervalMinutes, ViewModel.WutheringWavesAutoRefreshEnabled);
        ConfigureAutoRefreshTimer(_yihuanAutoRefreshTimer, ViewModel.YihuanAutoRefreshIntervalMinutes, ViewModel.YihuanAutoRefreshEnabled);
    }

    private void ConfigureAutoRefreshTimer(DispatcherTimer timer, double minutes, bool enabled)
    {
        timer.Stop();
        timer.Interval = TimeSpan.FromMinutes(Math.Clamp(double.IsNaN(minutes) ? 5 : minutes, 1, 180));
        if (enabled)
        {
            timer.Start();
        }
    }

    private void UpdateAutoSignTimer()
    {
        _autoSignTimer.Stop();
        _autoSignTimer.Interval = TimeSpan.FromSeconds(30);
        if (ViewModel.DailyAutoSignEnabled)
        {
            _autoSignTimer.Start();
        }
    }

    private void UpdateNotificationRuleTimer()
    {
        _notificationRuleTimer.Stop();
        _notificationRuleTimer.Interval = TimeSpan.FromMinutes(1);
        _notificationRuleTimer.Start();
    }

    private void SetDefaultSize()
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        AppWindow.GetFromWindowId(windowId).Resize(new SizeInt32(1380, 860));
    }

    private void ApplyThemePalette()
    {
        if (ViewModel.UseDarkTheme)
        {
            SetBrush("AppBackgroundBrush", "#202124");
            SetBrush("PanelSurfaceBrush", "#28292B");
            SetBrush("CardSurfaceBrush", "#303133");
            SetBrush("SubtleCardBrush", "#37383A");
            SetBrush("AppAccentBrush", "#4CC2FF");
            SetBrush("AppAccentSoftBrush", "#163447");
            SetBrush("AppBorderBrush", "#3E4043");
            SetBrush("AppTextBrush", "#F4F6F8");
            SetBrush("AppMutedTextBrush", "#A8ADB4");
            SetBrush("AppButtonBrush", "#37383A");
            SetBrush("AppButtonHoverBrush", "#424446");
            return;
        }

        SetBrush("AppBackgroundBrush", "#D7D7D7");
        SetBrush("PanelSurfaceBrush", "#E3E3E3");
        SetBrush("CardSurfaceBrush", "#F2F2F2");
        SetBrush("SubtleCardBrush", "#ECECEC");
        SetBrush("AppAccentBrush", "#0F78B8");
        SetBrush("AppAccentSoftBrush", "#D7EBF8");
        SetBrush("AppBorderBrush", "#C3C7CC");
        SetBrush("AppTextBrush", "#171A1F");
        SetBrush("AppMutedTextBrush", "#5D636B");
        SetBrush("AppButtonBrush", "#F6F6F6");
        SetBrush("AppButtonHoverBrush", "#ECEFF2");
    }

    private void SetBrush(string key, string hex)
    {
        if (Root.Resources[key] is SolidColorBrush brush)
        {
            brush.Color = ColorFromHex(hex);
            if (key == "AppBackgroundBrush")
            {
                Root.Background = brush;
            }
        }
    }

    private static Color ColorFromHex(string hex)
    {
        var value = hex.TrimStart('#');
        return Color.FromArgb(
            255,
            Convert.ToByte(value[..2], 16),
            Convert.ToByte(value.Substring(2, 2), 16),
            Convert.ToByte(value.Substring(4, 2), 16));
    }

    private void MainWindow_OnClosed(object sender, WindowEventArgs args)
    {
        _arknightsAutoRefreshTimer.Stop();
        _endfieldAutoRefreshTimer.Stop();
        _wutheringWavesAutoRefreshTimer.Stop();
        _yihuanAutoRefreshTimer.Stop();
        _autoSignTimer.Stop();
        _notificationRuleTimer.Stop();
        ViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        ViewModel.CredentialApplied -= ViewModel_OnCredentialApplied;
        _arknightsAutoRefreshTimer.Tick -= ArknightsAutoRefreshTimer_OnTick;
        _endfieldAutoRefreshTimer.Tick -= EndfieldAutoRefreshTimer_OnTick;
        _wutheringWavesAutoRefreshTimer.Tick -= WutheringWavesAutoRefreshTimer_OnTick;
        _yihuanAutoRefreshTimer.Tick -= YihuanAutoRefreshTimer_OnTick;
        _autoSignTimer.Tick -= AutoSignTimer_OnTick;
        _notificationRuleTimer.Tick -= NotificationRuleTimer_OnTick;
    }
}
