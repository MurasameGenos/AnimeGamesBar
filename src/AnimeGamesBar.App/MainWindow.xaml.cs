using AnimeGamesBar.App.ViewModels;
using Microsoft.UI.Xaml;

namespace AnimeGamesBar.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        Root.DataContext = ViewModel;
    }

    public MainViewModel ViewModel { get; }

    private void TokenBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Token = TokenBox.Password;
    }

    private void CookieBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Cookie = CookieBox.Password;
    }
}
