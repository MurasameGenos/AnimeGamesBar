using System.ComponentModel;
using AnimeGamesBar.App.ViewModels;
using Microsoft.UI.Xaml;

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

    private void MainWindow_OnClosed(object sender, WindowEventArgs args)
    {
        _autoRefreshTimer.Stop();
        ViewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        ViewModel.CredentialApplied -= ViewModel_OnCredentialApplied;
        _autoRefreshTimer.Tick -= AutoRefreshTimer_OnTick;
    }
}
