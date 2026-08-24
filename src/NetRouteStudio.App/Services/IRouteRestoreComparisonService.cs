using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IRouteRestoreComparisonService
{
    IReadOnlyList<RouteRestoreDiffItem> Compare(
        NetworkBackupDocument backup,
        IReadOnlyList<RouteInfo> currentRoutes,
        IReadOnlyList<NetworkAdapterInfo> currentAdapters);
}
