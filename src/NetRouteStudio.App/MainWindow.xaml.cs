using System.Windows;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App;

public partial class MainWindow : Window
{
    private readonly RouteManagementViewModel _viewModel;

    public MainWindow(RouteManagementViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.RefreshAsync();
    }
}
