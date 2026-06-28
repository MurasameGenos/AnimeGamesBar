using AnimeGamesBar.App.ViewModels;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        ViewModel.OwnerWindow = this;
        ViewModel.CredentialApplied += ViewModel_OnCredentialApplied;
        Root.DataContext = ViewModel;
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
}
