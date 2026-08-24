using CommunityToolkit.Mvvm.ComponentModel;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Models;

public sealed partial class BatchRouteEditItem : ObservableObject
{
    public RouteInfo? OriginalRoute { get; init; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private BatchRouteOperation _operation;
    [ObservableProperty] private string _destinationPrefix = string.Empty;
    [ObservableProperty] private string _nextHop = string.Empty;
    [ObservableProperty] private string _interfaceIndex = string.Empty;
    [ObservableProperty] private string _routeMetric = "0";
    [ObservableProperty] private bool _isPersistent;
    [ObservableProperty] private NetworkAdapterInfo? _selectedAdapter;

    public string OperationDisplay => Operation switch
    {
        BatchRouteOperation.Create => "新增",
        BatchRouteOperation.Update => "修改",
        _ => "删除"
    };

    public IPv4RouteRequest BuildRequest()
    {
        if (!int.TryParse(InterfaceIndex, out var interfaceIndex) ||
            !int.TryParse(RouteMetric, out var routeMetric))
        {
            throw new ArgumentException("接口索引和路由 Metric 必须是整数。");
        }

        return IPv4RouteValidator.ValidateAndNormalize(new IPv4RouteRequest(
            DestinationPrefix, NextHop, interfaceIndex, routeMetric, IsPersistent));
    }

    public static BatchRouteEditItem FromRoute(RouteInfo route) => new()
    {
        OriginalRoute = route,
        Operation = BatchRouteOperation.Update,
        DestinationPrefix = route.DestinationPrefix,
        NextHop = route.NextHop,
        InterfaceIndex = route.InterfaceIndex.ToString(),
        RouteMetric = route.RouteMetric.ToString(),
        IsPersistent = route.IsPersistent
    };

    partial void OnSelectedAdapterChanged(NetworkAdapterInfo? value)
    {
        if (value is null)
        {
            InterfaceIndex = string.Empty;
            return;
        }

        InterfaceIndex = value.InterfaceIndex.ToString();
        if (string.IsNullOrWhiteSpace(NextHop))
        {
            NextHop = value.Gateways.FirstOrDefault(gateway => !gateway.Contains(':')) ?? "0.0.0.0";
        }
    }
}
