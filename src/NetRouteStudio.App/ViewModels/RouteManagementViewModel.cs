using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.ViewModels;

public sealed partial class RouteManagementViewModel(
    IRouteTableService routeTableService,
    INetworkAdapterService networkAdapterService,
    IIPv4RouteManagementService managementService,
    IConfirmationService confirmationService,
    ILogger<RouteManagementViewModel> logger) : ObservableObject
{
    private readonly List<RouteInfo> _allRoutes = [];

    [ObservableProperty] private RouteInfo? _selectedRoute;
    [ObservableProperty] private string _destinationPrefix = string.Empty;
    [ObservableProperty] private string _nextHop = string.Empty;
    [ObservableProperty] private string _interfaceIndex = string.Empty;
    [ObservableProperty] private string _routeMetric = "0";
    [ObservableProperty] private bool _isPersistent;
    [ObservableProperty] private string _statusMessage = "等待读取可管理的 IPv4 路由";
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private NetworkAdapterInfo? _selectedAdapter;
    [ObservableProperty] private string _searchText = string.Empty;

    public ObservableCollection<RouteInfo> Routes { get; } = [];
    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = [];
    public ObservableCollection<string> NextHopOptions { get; } = [];

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var routesTask = routeTableService.GetRoutesAsync();
            var adaptersTask = networkAdapterService.GetAdaptersAsync();
            await Task.WhenAll(routesTask, adaptersTask);
            var routes = await routesTask;
            var adapters = await adaptersTask;
            _allRoutes.Clear();
            _allRoutes.AddRange(routes.Where(route => route.AddressFamily == RouteAddressFamily.IPv4));
            ApplySearch();

            Adapters.Clear();
            foreach (var adapter in adapters.OrderBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Adapters.Add(adapter);
            }

            NextHopOptions.Clear();
            NextHopOptions.Add("0.0.0.0");
            foreach (var gateway in adapters.SelectMany(adapter => adapter.Gateways)
                         .Where(gateway => !string.IsNullOrWhiteSpace(gateway) && !gateway.Contains(':'))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(gateway => gateway, StringComparer.OrdinalIgnoreCase))
            {
                NextHopOptions.Add(gateway);
            }

            UpdateStatusMessage();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "读取 IPv4 路由管理数据失败");
            ErrorMessage = exception.Message;
            StatusMessage = "路由读取失败";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        await ExecuteAsync(
            request => managementService.CreateAsync(request),
            result => ApplyVerifiedRoute(null, result.VerifiedRoute));
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (SelectedRoute is null)
        {
            ErrorMessage = "请先选择要修改的路由。";
            return;
        }

        IPv4RouteRequest request;
        try
        {
            request = BuildRequest();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            return;
        }

        var existingRoute = SelectedRoute;
        var message = $"确认修改路由？\n\n原目标：{SelectedRoute.DestinationPrefix}\n新目标：{request.DestinationPrefix}\n" +
                      $"下一跳：{request.NextHop}\n接口索引：{request.InterfaceIndex}\n" +
                      $"生效范围：{(request.IsPersistent ? "永久" : "临时")}";
        if (!confirmationService.Confirm("确认修改 IPv4 路由", message))
        {
            return;
        }

        await ExecuteAsync(
            normalizedRequest => managementService.UpdateAsync(existingRoute, normalizedRequest),
            result => ApplyVerifiedRoute(existingRoute, result.VerifiedRoute),
            request);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedRoute is null)
        {
            ErrorMessage = "请先选择要删除的路由。";
            return;
        }

        var message = $"确认删除路由？\n\n目标：{SelectedRoute.DestinationPrefix}\n" +
                      $"下一跳：{SelectedRoute.NextHop}\n接口：{SelectedRoute.InterfaceAlias}（{SelectedRoute.InterfaceIndex}）\n" +
                      $"生效范围：{SelectedRoute.LifetimeDisplay}";
        if (!confirmationService.Confirm("确认删除 IPv4 路由", message))
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var routeToDelete = SelectedRoute;
            var result = await managementService.DeleteAsync(routeToDelete);
            _allRoutes.Remove(routeToDelete);
            ApplySearch();
            SelectedRoute = null;
            StatusMessage = result.Message;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "删除 IPv4 路由失败：{DestinationPrefix}", SelectedRoute?.DestinationPrefix);
            ErrorMessage = exception.Message;
            StatusMessage = "路由删除失败";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ClearForm()
    {
        SelectedRoute = null;
        DestinationPrefix = string.Empty;
        NextHop = string.Empty;
        InterfaceIndex = string.Empty;
        SelectedAdapter = null;
        RouteMetric = "0";
        IsPersistent = false;
        ErrorMessage = string.Empty;
    }

    partial void OnSelectedRouteChanged(RouteInfo? value)
    {
        if (value is null)
        {
            return;
        }

        DestinationPrefix = value.DestinationPrefix;
        NextHop = value.NextHop == "0.0.0.0" ? string.Empty : value.NextHop;
        InterfaceIndex = value.InterfaceIndex.ToString();
        SelectedAdapter = Adapters.FirstOrDefault(adapter => adapter.InterfaceIndex == value.InterfaceIndex);
        RouteMetric = value.RouteMetric.ToString();
        IsPersistent = value.IsPersistent;
    }

    partial void OnSelectedAdapterChanged(NetworkAdapterInfo? value)
    {
        if (value is not null)
        {
            InterfaceIndex = value.InterfaceIndex.ToString();
            if (SelectedRoute is null && string.IsNullOrWhiteSpace(NextHop))
            {
                NextHop = value.Gateways.FirstOrDefault(gateway => !gateway.Contains(':')) ?? "0.0.0.0";
            }
        }
    }

    partial void OnSearchTextChanged(string value) => ApplySearch();

    private async Task ExecuteAsync(
        Func<IPv4RouteRequest, Task<RouteMutationResult>> action,
        Action<RouteMutationResult> applyResult,
        IPv4RouteRequest? existingRequest = null)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var request = existingRequest ?? BuildRequest();
            var result = await action(request);
            applyResult(result);
            StatusMessage = result.Message;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "IPv4 路由操作失败");
            ErrorMessage = exception.Message;
            StatusMessage = "IPv4 路由操作失败";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private IPv4RouteRequest BuildRequest()
    {
        if (!int.TryParse(InterfaceIndex, out var parsedInterfaceIndex) ||
            !int.TryParse(RouteMetric, out var parsedMetric))
        {
            throw new ArgumentException("接口索引和路由跃点必须是整数。");
        }

        return IPv4RouteValidator.ValidateAndNormalize(new IPv4RouteRequest(
            DestinationPrefix,
            NextHop,
            parsedInterfaceIndex,
            parsedMetric,
            IsPersistent));
    }

    private void ApplyVerifiedRoute(RouteInfo? previousRoute, RouteInfo? verifiedRoute)
    {
        if (previousRoute is not null)
        {
            _allRoutes.Remove(previousRoute);
        }

        if (verifiedRoute is not null)
        {
            _allRoutes.RemoveAll(route => IsSameIdentity(route, verifiedRoute));
            _allRoutes.Add(verifiedRoute);
        }

        ApplySearch();
        SelectedRoute = verifiedRoute;
    }

    private void ApplySearch()
    {
        IEnumerable<RouteInfo> filtered = _allRoutes;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            filtered = filtered.Where(route =>
                route.DestinationPrefix.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.NextHop.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.InterfaceAlias.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.InterfaceIndex.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.RouteMetric.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.InterfaceMetric.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.EffectiveMetric.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.LifetimeDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                route.OperabilityDisplay.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        Routes.Clear();
        foreach (var route in filtered.OrderBy(route => route.DestinationPrefix, StringComparer.OrdinalIgnoreCase)
                     .ThenBy(route => route.EffectiveMetric))
        {
            Routes.Add(route);
        }

        UpdateStatusMessage();
    }

    private void UpdateStatusMessage()
    {
        StatusMessage = $"共 {_allRoutes.Count} 条 IPv4 路由，当前显示 {Routes.Count} 条，{Adapters.Count} 个可选接口";
    }

    private static bool IsSameIdentity(RouteInfo left, RouteInfo right) =>
        left.DestinationPrefix == right.DestinationPrefix &&
        left.NextHop == right.NextHop &&
        left.InterfaceIndex == right.InterfaceIndex &&
        left.IsPersistent == right.IsPersistent;
}
