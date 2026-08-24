using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.ViewModels;

public sealed partial class RouteManagementViewModel(
    IRouteTableService routeTableService,
    IIPv4RouteManagementService managementService,
    IConfirmationService confirmationService) : ObservableObject
{
    [ObservableProperty] private RouteInfo? _selectedRoute;
    [ObservableProperty] private string _destinationPrefix = string.Empty;
    [ObservableProperty] private string _nextHop = string.Empty;
    [ObservableProperty] private string _interfaceIndex = string.Empty;
    [ObservableProperty] private string _routeMetric = "0";
    [ObservableProperty] private bool _isPersistent;
    [ObservableProperty] private string _statusMessage = "等待读取可管理的 IPv4 路由";
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<RouteInfo> Routes { get; } = [];

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
            var routes = await routeTableService.GetRoutesAsync();
            Routes.Clear();
            foreach (var route in routes.Where(route =>
                         route.AddressFamily == RouteAddressFamily.IPv4 && route.IsUserOperable))
            {
                Routes.Add(route);
            }

            StatusMessage = $"共 {Routes.Count} 条用户可操作的 IPv4 路由";
        }
        catch (Exception exception)
        {
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
        await ExecuteAsync(async request =>
        {
            var result = await managementService.CreateAsync(request);
            StatusMessage = result.Message;
        });
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (SelectedRoute is null)
        {
            ErrorMessage = "请先选择要修改的路由。";
            return;
        }

        var request = BuildRequest();
        var message = $"确认修改路由？\n\n原目标：{SelectedRoute.DestinationPrefix}\n新目标：{request.DestinationPrefix}\n" +
                      $"下一跳：{request.NextHop}\n接口索引：{request.InterfaceIndex}\n" +
                      $"生效范围：{(request.IsPersistent ? "永久" : "临时")}";
        if (!confirmationService.Confirm("确认修改 IPv4 路由", message))
        {
            return;
        }

        await ExecuteAsync(async normalizedRequest =>
        {
            var result = await managementService.UpdateAsync(SelectedRoute, normalizedRequest);
            StatusMessage = result.Message;
        }, request);
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
            var result = await managementService.DeleteAsync(SelectedRoute);
            StatusMessage = result.Message;
            SelectedRoute = null;
            await RefreshAsyncAfterMutation();
        }
        catch (Exception exception)
        {
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
        RouteMetric = value.RouteMetric.ToString();
        IsPersistent = value.IsPersistent;
    }

    private async Task ExecuteAsync(
        Func<IPv4RouteRequest, Task> action,
        IPv4RouteRequest? existingRequest = null)
    {
        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var request = existingRequest ?? BuildRequest();
            await action(request);
            await RefreshAsyncAfterMutation();
        }
        catch (Exception exception)
        {
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

    private async Task RefreshAsyncAfterMutation()
    {
        var routes = await routeTableService.GetRoutesAsync();
        Routes.Clear();
        foreach (var route in routes.Where(route =>
                     route.AddressFamily == RouteAddressFamily.IPv4 && route.IsUserOperable))
        {
            Routes.Add(route);
        }
    }
}
