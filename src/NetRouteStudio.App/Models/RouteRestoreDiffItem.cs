using CommunityToolkit.Mvvm.ComponentModel;

namespace NetRouteStudio.App.Models;

public sealed partial class RouteRestoreDiffItem : ObservableObject
{
    public required RouteInfo? BackupRoute { get; init; }

    [ObservableProperty] private RouteInfo? _currentRoute;
    [ObservableProperty] private NetworkAdapterInfo? _selectedAdapter;
    [ObservableProperty] private RouteRestoreDifferenceKind _differenceKind;
    [ObservableProperty] private bool _isSelected;

    public string DifferenceDisplay => DifferenceKind switch
    {
        RouteRestoreDifferenceKind.Missing => "当前缺失",
        RouteRestoreDifferenceKind.Changed => "配置不同",
        RouteRestoreDifferenceKind.Same => "完全一致",
        _ => "仅当前存在（忽略）"
    };

    public string RiskDisplay => BackupRoute is { IsUserOperable: false }
        ? "系统路由｜手动确认"
        : BackupRoute is null ? "不会删除" : "用户路由";

    public bool CanRestore => BackupRoute is not null &&
                              DifferenceKind is RouteRestoreDifferenceKind.Missing or RouteRestoreDifferenceKind.Changed;

    public string DestinationPrefix => BackupRoute?.DestinationPrefix ?? CurrentRoute?.DestinationPrefix ?? "—";

    public string NextHop => BackupRoute?.NextHop ?? CurrentRoute?.NextHop ?? "—";

    public string BackupMetric => BackupRoute?.RouteMetric.ToString() ?? "—";

    public string CurrentMetric => CurrentRoute?.RouteMetric.ToString() ?? "—";

    public string BackupLifetime => BackupRoute?.LifetimeDisplay ?? "—";

    public string CurrentLifetime => CurrentRoute?.LifetimeDisplay ?? "—";

    partial void OnDifferenceKindChanged(RouteRestoreDifferenceKind value)
    {
        OnPropertyChanged(nameof(DifferenceDisplay));
        OnPropertyChanged(nameof(CanRestore));
    }

    partial void OnCurrentRouteChanged(RouteInfo? value)
    {
        OnPropertyChanged(nameof(CurrentMetric));
        OnPropertyChanged(nameof(CurrentLifetime));
    }
}
