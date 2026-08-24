using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class RouteRestoreComparisonService : IRouteRestoreComparisonService
{
    public IReadOnlyList<RouteRestoreDiffItem> Compare(
        NetworkBackupDocument backup,
        IReadOnlyList<RouteInfo> currentRoutes,
        IReadOnlyList<NetworkAdapterInfo> currentAdapters)
    {
        var ipv4Routes = currentRoutes.Where(route => route.AddressFamily == RouteAddressFamily.IPv4).ToArray();
        var matchedCurrent = new HashSet<RouteInfo>();
        var result = new List<RouteRestoreDiffItem>();

        foreach (var backupRoute in backup.Routes)
        {
            var adapter = MatchAdapter(backupRoute, backup.Adapters, currentAdapters);
            var current = FindCurrentRoute(backupRoute, adapter, ipv4Routes);
            if (current is not null)
            {
                matchedCurrent.Add(current);
            }

            var difference = current is null
                ? RouteRestoreDifferenceKind.Missing
                : IsEquivalent(backupRoute, current, adapter)
                    ? RouteRestoreDifferenceKind.Same
                    : RouteRestoreDifferenceKind.Changed;
            result.Add(new RouteRestoreDiffItem
            {
                BackupRoute = backupRoute,
                CurrentRoute = current,
                SelectedAdapter = adapter,
                DifferenceKind = difference,
                IsSelected = backupRoute.IsUserOperable && adapter is not null &&
                             difference is RouteRestoreDifferenceKind.Missing or RouteRestoreDifferenceKind.Changed
            });
        }

        foreach (var current in ipv4Routes.Where(route => !matchedCurrent.Contains(route)))
        {
            result.Add(new RouteRestoreDiffItem
            {
                BackupRoute = null,
                CurrentRoute = current,
                SelectedAdapter = currentAdapters.FirstOrDefault(adapter => adapter.InterfaceIndex == current.InterfaceIndex),
                DifferenceKind = RouteRestoreDifferenceKind.CurrentOnly,
                IsSelected = false
            });
        }

        return result
            .OrderBy(item => GetDifferencePriority(item.DifferenceKind))
            .ThenByDescending(item => item.IsSelected)
            .ThenBy(item => (item.BackupRoute ?? item.CurrentRoute)?.IsUserOperable == false ? 1 : 0)
            .ThenBy(item => item.DestinationPrefix, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.NextHop, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => (item.BackupRoute ?? item.CurrentRoute)?.InterfaceIndex ?? int.MaxValue)
            .ToArray();
    }

    private static int GetDifferencePriority(RouteRestoreDifferenceKind differenceKind) => differenceKind switch
    {
        RouteRestoreDifferenceKind.Missing => 0,
        RouteRestoreDifferenceKind.Changed => 1,
        RouteRestoreDifferenceKind.CurrentOnly => 2,
        RouteRestoreDifferenceKind.Same => 3,
        RouteRestoreDifferenceKind.Deleted => 4,
        _ => int.MaxValue
    };

    private static NetworkAdapterInfo? MatchAdapter(
        RouteInfo route,
        IReadOnlyList<NetworkAdapterInfo> backupAdapters,
        IReadOnlyList<NetworkAdapterInfo> currentAdapters)
    {
        var backupAdapter = backupAdapters.FirstOrDefault(adapter => adapter.InterfaceIndex == route.InterfaceIndex);
        if (backupAdapter is not null)
        {
            return currentAdapters.FirstOrDefault(adapter =>
                       adapter.InterfaceIndex == backupAdapter.InterfaceIndex &&
                       adapter.Name.Equals(backupAdapter.Name, StringComparison.OrdinalIgnoreCase))
                   ?? currentAdapters.FirstOrDefault(adapter =>
                       adapter.Name.Equals(backupAdapter.Name, StringComparison.OrdinalIgnoreCase));
        }

        return currentAdapters.FirstOrDefault(adapter =>
                   adapter.InterfaceIndex == route.InterfaceIndex &&
                   adapter.Name.Equals(route.InterfaceAlias, StringComparison.OrdinalIgnoreCase))
               ?? currentAdapters.FirstOrDefault(adapter =>
                   adapter.Name.Equals(route.InterfaceAlias, StringComparison.OrdinalIgnoreCase));
    }

    private static RouteInfo? FindCurrentRoute(
        RouteInfo backupRoute,
        NetworkAdapterInfo? adapter,
        IReadOnlyList<RouteInfo> currentRoutes) =>
        currentRoutes.FirstOrDefault(route =>
            route.DestinationPrefix == backupRoute.DestinationPrefix &&
            route.NextHop == backupRoute.NextHop &&
            (adapter is not null
                ? route.InterfaceIndex == adapter.InterfaceIndex
                : route.InterfaceAlias.Equals(backupRoute.InterfaceAlias, StringComparison.OrdinalIgnoreCase)));

    private static bool IsEquivalent(
        RouteInfo backupRoute,
        RouteInfo currentRoute,
        NetworkAdapterInfo? adapter)
    {
        var interfaceMatches = adapter is not null
            ? currentRoute.InterfaceIndex == adapter.InterfaceIndex
            : currentRoute.InterfaceIndex == backupRoute.InterfaceIndex &&
              currentRoute.InterfaceAlias.Equals(backupRoute.InterfaceAlias, StringComparison.OrdinalIgnoreCase);
        return interfaceMatches &&
        currentRoute.RouteMetric == backupRoute.RouteMetric &&
        currentRoute.IsPersistent == backupRoute.IsPersistent;
    }
}
