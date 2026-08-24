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
        RouteRestoreDifferenceKind.CurrentOnly => "仅当前存在",
        _ => "已删除"
    };

    public string RiskDisplay => (BackupRoute ?? CurrentRoute) is { IsUserOperable: false }
        ? BackupRoute is null ? "系统路由｜手动删除" : "系统路由｜手动确认"
        : BackupRoute is null ? "当前额外路由｜手动删除" : "用户路由";

    public bool CanRestore => DifferenceKind switch
    {
        RouteRestoreDifferenceKind.Missing or RouteRestoreDifferenceKind.Changed => BackupRoute is not null,
        RouteRestoreDifferenceKind.CurrentOnly => CurrentRoute is not null,
        _ => false
    };

    public bool CanSelectAdapter => DifferenceKind is RouteRestoreDifferenceKind.Missing or RouteRestoreDifferenceKind.Changed;

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
        OnPropertyChanged(nameof(CanSelectAdapter));
    }

    partial void OnCurrentRouteChanged(RouteInfo? value)
    {
        OnPropertyChanged(nameof(CurrentMetric));
        OnPropertyChanged(nameof(CurrentLifetime));
    }
}
