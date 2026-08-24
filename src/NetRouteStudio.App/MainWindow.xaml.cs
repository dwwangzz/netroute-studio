using System.Windows;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App;

public partial class MainWindow : Window
{
    private readonly RouteManagementViewModel _viewModel;
    private readonly IIPv4InterfaceMetricDialogService _metricDialogService;

    public MainWindow(
        RouteManagementViewModel viewModel,
        IIPv4InterfaceMetricDialogService metricDialogService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _metricDialogService = metricDialogService;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.RefreshAsync();
    }

    private void OnOpenIPv4InterfaceMetric(object sender, RoutedEventArgs e) =>
        _metricDialogService.Show();
}
