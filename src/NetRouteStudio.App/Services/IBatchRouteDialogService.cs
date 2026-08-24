using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IBatchRouteDialogService
{
    IReadOnlyList<BatchRouteEditItem>? Edit(
        IReadOnlyList<RouteInfo> routes,
        IReadOnlyList<NetworkAdapterInfo> adapters);

    void ShowResults(IReadOnlyList<BatchRouteExecutionResult> results);
}
