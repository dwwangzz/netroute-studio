using System.Windows;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App;

public partial class IPv4InterfaceMetricWindow : Window
{
    private readonly IPv4InterfaceMetricViewModel _viewModel;

    public IPv4InterfaceMetricWindow(IPv4InterfaceMetricViewModel viewModel)
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
