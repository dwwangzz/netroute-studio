using System.Windows;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App;

public partial class MainWindow : Window
{
    private readonly RouteManagementViewModel _viewModel;
    private readonly IIPv4InterfaceMetricDialogService _metricDialogService;
    private readonly IRouteBackupDialogService _backupDialogService;
    private readonly IIPv6ResetDialogService _ipv6ResetDialogService;
    private readonly INetworkTestDialogService _networkTestDialogService;
    private readonly IControlledCommandDialogService _controlledCommandDialogService;

    public MainWindow(
        RouteManagementViewModel viewModel,
        IIPv4InterfaceMetricDialogService metricDialogService,
        IRouteBackupDialogService backupDialogService,
        IIPv6ResetDialogService ipv6ResetDialogService,
        INetworkTestDialogService networkTestDialogService,
        IControlledCommandDialogService controlledCommandDialogService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _metricDialogService = metricDialogService;
        _backupDialogService = backupDialogService;
        _ipv6ResetDialogService = ipv6ResetDialogService;
        _networkTestDialogService = networkTestDialogService;
        _controlledCommandDialogService = controlledCommandDialogService;
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

    private void OnOpenRouteBackup(object sender, RoutedEventArgs e) =>
        _backupDialogService.Show();

    private void OnOpenIPv6Reset(object sender, RoutedEventArgs e) =>
        _ipv6ResetDialogService.Show();

    private void OnOpenNetworkTest(object sender, RoutedEventArgs e) =>
        _networkTestDialogService.Show();

    private void OnOpenControlledCommand(object sender, RoutedEventArgs e) =>
        _controlledCommandDialogService.Show();
}
