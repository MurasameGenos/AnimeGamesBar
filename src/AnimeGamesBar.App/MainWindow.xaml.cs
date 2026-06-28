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
    private readonly DispatcherTimer _autoRefreshTimer = new();

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        ViewModel.OwnerWindow = this;
        ViewModel.CredentialApplied += ViewModel_OnCredentialApplied;
        ViewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _autoRefreshTimer.Tick += AutoRefreshTimer_OnTick;
        Root.DataContext = ViewModel;
        Closed += MainWindow_OnClosed;
        SetDefaultSize();
        ApplyThemePalette();
        UpdateAutoRefreshTimer();
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
        if (e.PropertyName is nameof(MainViewModel.AutoRefreshEnabled) or nameof(MainViewModel.AutoRefreshIntervalMinutes))
        {
            UpdateAutoRefreshTimer();
        }

        if (e.PropertyName == nameof(MainViewModel.RootTheme))
        {
            ApplyThemePalette();
        }
    }

    private void AutoRefreshTimer_OnTick(object? sender, object e)
    {
        if (ViewModel.AutoRefreshEnabled && ViewModel.RefreshAllCommand.CanExecute(null))
        {
            ViewModel.RefreshAllCommand.Execute(null);
        }
    }

    private void UpdateAutoRefreshTimer()
    {
        _autoRefreshTimer.Stop();
        _autoRefreshTimer.Interval = TimeSpan.FromMinutes(ViewModel.AutoRefreshIntervalMinutes);

        if (ViewModel.AutoRefreshEnabled)
        {
            _autoRefreshTimer.Start();
        }
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
        _autoRefreshTimer.Stop();
        ViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        ViewModel.CredentialApplied -= ViewModel_OnCredentialApplied;
        _autoRefreshTimer.Tick -= AutoRefreshTimer_OnTick;
    }
}
