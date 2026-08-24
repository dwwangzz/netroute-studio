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
            .OrderBy(item => item.DifferenceKind)
            .ThenBy(item => item.DestinationPrefix, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

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
