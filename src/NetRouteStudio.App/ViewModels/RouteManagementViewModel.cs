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
    IBatchRouteDialogService batchDialogService,
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

        if (!confirmationService.Confirm(BuildConfirmation(
                "确认新增 IPv4 路由",
                "新增 IPv4 路由",
                null,
                request,
                managementService.GetCreateCommand(request))))
        {
            return;
        }

        await ExecuteAsync(
            normalizedRequest => managementService.CreateAsync(normalizedRequest),
            result => ApplyVerifiedRoute(null, result.VerifiedRoute),
            request);
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
        if (!confirmationService.Confirm(BuildConfirmation(
                "确认修改 IPv4 路由",
                "修改 IPv4 路由",
                existingRoute,
                request,
                managementService.GetUpdateCommand(existingRoute, request))))
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

        var routeToDelete = SelectedRoute;
        if (!confirmationService.Confirm(BuildConfirmation(
                "确认删除 IPv4 路由",
                "删除 IPv4 路由",
                routeToDelete,
                null,
                managementService.GetDeleteCommand(routeToDelete))))
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
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
    private async Task BatchManageAsync()
    {
        var items = batchDialogService.Edit(_allRoutes, Adapters);
        if (items is null || items.Count == 0)
        {
            return;
        }

        RouteConfirmationRequest confirmation;
        try
        {
            confirmation = BuildBatchConfirmation(items);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            return;
        }

        if (!confirmationService.Confirm(confirmation))
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        var results = new List<BatchRouteExecutionResult>();
        try
        {
            foreach (var item in items)
            {
                await ExecuteBatchItemAsync(item, results);
            }

            ApplySearch();
            var succeeded = results.Count(result => result.Succeeded);
            StatusMessage = $"批量路由操作完成：成功 {succeeded} 条，失败 {results.Count - succeeded} 条。";
            if (succeeded != results.Count)
            {
                ErrorMessage = "部分路由操作失败，请在结果窗口中查看每条记录。";
            }
        }
        finally
        {
            IsLoading = false;
            batchDialogService.ShowResults(results);
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

    private RouteConfirmationRequest BuildConfirmation(
        string title,
        string operationName,
        RouteInfo? before,
        IPv4RouteRequest? after,
        string command)
    {
        var afterAdapter = after is null
            ? null
            : Adapters.FirstOrDefault(adapter => adapter.InterfaceIndex == after.InterfaceIndex);
        var afterInterfaceMetric = afterAdapter?.IPv4InterfaceMetric ?? 0;
        const string empty = "—";

        string Before(Func<RouteInfo, string> selector) => before is null ? empty : selector(before);
        string After(Func<string> selector) => after is null ? empty : selector();

        RouteConfirmationField[] fields =
        [
            new("地址族", Before(route => route.AddressFamilyDisplay), After(() => "IPv4")),
            new("目标网络", Before(route => route.DestinationPrefix), After(() => after!.DestinationPrefix)),
            new("下一跳", Before(route => DisplayNextHop(route.NextHop)), After(() => DisplayNextHop(after!.NextHop))),
            new("网卡接口", Before(route => route.InterfaceAlias), After(() => afterAdapter?.Name ?? $"接口 {after!.InterfaceIndex}")),
            new("接口索引", Before(route => route.InterfaceIndex.ToString()), After(() => after!.InterfaceIndex.ToString())),
            new("路由 Metric", Before(route => route.RouteMetric.ToString()), After(() => after!.RouteMetric.ToString())),
            new("接口 Metric", Before(route => route.InterfaceMetric.ToString()), After(() => afterInterfaceMetric.ToString())),
            new("有效 Metric", Before(route => route.EffectiveMetric.ToString()), After(() => (after!.RouteMetric + afterInterfaceMetric).ToString())),
            new("保存方式", Before(route => route.LifetimeDisplay), After(() => after!.IsPersistent ? "永久" : "临时")),
            new("活动状态", Before(route => route.IsActive ? "已生效" : "未生效"), After(() => "执行后由系统确认")),
            new("协议/来源", Before(route => route.Protocol), After(() => "NetMgmt")),
            new("管理属性", Before(route => route.OperabilityDisplay), After(() => "用户可操作"))
        ];

        return new RouteConfirmationRequest(title, operationName, fields, command.Trim());
    }

    private RouteConfirmationRequest BuildBatchConfirmation(IReadOnlyList<BatchRouteEditItem> items)
    {
        var fields = new List<RouteConfirmationField>();
        var commands = new List<string>();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var request = item.Operation == BatchRouteOperation.Delete ? null : item.BuildRequest();
            var command = item.Operation switch
            {
                BatchRouteOperation.Create => managementService.GetCreateCommand(request!),
                BatchRouteOperation.Update => managementService.GetUpdateCommand(item.OriginalRoute!, request!),
                _ => managementService.GetDeleteCommand(item.OriginalRoute!)
            };
            var single = BuildConfirmation(string.Empty, string.Empty, item.OriginalRoute, request, command);
            var prefix = $"[{index + 1} {item.OperationDisplay}] ";
            fields.AddRange(single.Fields.Select(field => field with { Name = prefix + field.Name }));
            commands.Add($"# {prefix}{item.DestinationPrefix}\n{command.Trim()}");
        }

        return new RouteConfirmationRequest(
            "确认批量 IPv4 路由操作",
            $"批量 IPv4 路由操作（共 {items.Count} 条）",
            fields,
            string.Join("\n\n", commands));
    }

    private async Task ExecuteBatchItemAsync(
        BatchRouteEditItem item,
        ICollection<BatchRouteExecutionResult> results)
    {
        try
        {
            RouteMutationResult result;
            switch (item.Operation)
            {
                case BatchRouteOperation.Create:
                    result = await managementService.CreateAsync(item.BuildRequest());
                    AddOrReplaceLocalRoute(result.VerifiedRoute);
                    break;
                case BatchRouteOperation.Update:
                    result = await managementService.UpdateAsync(item.OriginalRoute!, item.BuildRequest());
                    _allRoutes.Remove(item.OriginalRoute!);
                    AddOrReplaceLocalRoute(result.VerifiedRoute);
                    break;
                default:
                    result = await managementService.DeleteAsync(item.OriginalRoute!);
                    _allRoutes.Remove(item.OriginalRoute!);
                    break;
            }

            results.Add(new BatchRouteExecutionResult(
                item.Operation, item.DestinationPrefix, true, result.Message));
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "批量路由操作失败：{Operation} {DestinationPrefix}", item.Operation, item.DestinationPrefix);
            results.Add(new BatchRouteExecutionResult(
                item.Operation, item.DestinationPrefix, false, exception.Message));
        }
    }

    private void AddOrReplaceLocalRoute(RouteInfo? route)
    {
        if (route is null)
        {
            return;
        }

        _allRoutes.RemoveAll(existing => IsSameIdentity(existing, route));
        _allRoutes.Add(route);
    }

    private static string DisplayNextHop(string nextHop) =>
        nextHop == "0.0.0.0" ? "在链路上（0.0.0.0）" : nextHop;

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
