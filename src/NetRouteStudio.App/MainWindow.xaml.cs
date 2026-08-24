using System.Windows;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App;

public partial class MainWindow : Window
{
    private readonly RouteManagementViewModel _viewModel;
    private readonly MainWindowViewModel _foundationViewModel;
    private readonly RouteTableViewModel _routeTableViewModel;
    private readonly RouteMatchViewModel _routeMatchViewModel;
    private readonly ApplicationFoundationView _foundationView = new();
    private readonly NetworkAdapterView _adapterView = new();
    private readonly RouteTableView _routeTableView = new();
    private readonly RouteMatchView _routeMatchView = new();
    private readonly IIPv4InterfaceMetricDialogService _metricDialogService;
    private readonly IRouteBackupDialogService _backupDialogService;
    private readonly IIPv6ResetDialogService _ipv6ResetDialogService;
    private readonly INetworkTestDialogService _networkTestDialogService;
    private readonly IControlledCommandDialogService _controlledCommandDialogService;

    public MainWindow(
        RouteManagementViewModel viewModel,
        MainWindowViewModel foundationViewModel,
        RouteTableViewModel routeTableViewModel,
        RouteMatchViewModel routeMatchViewModel,
        IIPv4InterfaceMetricDialogService metricDialogService,
        IRouteBackupDialogService backupDialogService,
        IIPv6ResetDialogService ipv6ResetDialogService,
        INetworkTestDialogService networkTestDialogService,
        IControlledCommandDialogService controlledCommandDialogService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _foundationViewModel = foundationViewModel;
        _routeTableViewModel = routeTableViewModel;
        _routeMatchViewModel = routeMatchViewModel;
        _foundationView.DataContext = foundationViewModel;
        _adapterView.DataContext = foundationViewModel;
        _routeTableView.DataContext = routeTableViewModel;
        _routeMatchView.DataContext = routeMatchViewModel;
        _metricDialogService = metricDialogService;
        _backupDialogService = backupDialogService;
        _ipv6ResetDialogService = ipv6ResetDialogService;
        _networkTestDialogService = networkTestDialogService;
        _controlledCommandDialogService = controlledCommandDialogService;
        DataContext = viewModel;
        Highlight(RouteManagementButton);
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

    private void OnShowFoundation(object sender, RoutedEventArgs e) => ShowModule(_foundationView, FoundationButton, _foundationViewModel.StatusMessage);

    private async void OnShowAdapters(object sender, RoutedEventArgs e)
    {
        ShowModule(_adapterView, AdapterButton, "正在读取网卡信息…");
        await _foundationViewModel.RefreshAdaptersAsync();
        StatusText.Text = _foundationViewModel.StatusMessage;
    }

    private async void OnShowRouteTable(object sender, RoutedEventArgs e)
    {
        ShowModule(_routeTableView, RouteTableButton, "正在读取 Windows 路由表…");
        await _routeTableViewModel.RefreshRoutesAsync();
        StatusText.Text = _routeTableViewModel.StatusMessage;
    }

    private void OnShowRouteMatch(object sender, RoutedEventArgs e) => ShowModule(_routeMatchView, RouteMatchButton, "请输入 IP 地址或域名进行路由匹配");

    private void OnShowRouteManagement(object sender, RoutedEventArgs e)
    {
        ModuleContent.Visibility = Visibility.Collapsed;
        RouteManagementContent.Visibility = Visibility.Visible;
        StatusText.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new System.Windows.Data.Binding("StatusMessage"));
        Highlight(RouteManagementButton);
    }

    private void ShowModule(object content, System.Windows.Controls.Button button, string status)
    {
        StatusText.ClearValue(System.Windows.Controls.TextBlock.TextProperty);
        StatusText.Text = status;
        RouteManagementContent.Visibility = Visibility.Collapsed;
        ModuleContent.Content = content;
        ModuleContent.Visibility = Visibility.Visible;
        Highlight(button);
    }

    private void Highlight(System.Windows.Controls.Button active)
    {
        foreach (var button in new[] { FoundationButton, AdapterButton, RouteTableButton, RouteMatchButton, RouteManagementButton })
        {
            button.Background = button == active ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(219, 234, 254)) : System.Windows.Media.Brushes.Transparent;
            button.Foreground = button == active ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(29, 78, 216)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(75, 85, 99));
        }
    }
}
