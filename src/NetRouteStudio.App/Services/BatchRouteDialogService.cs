using System.Windows;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class BatchRouteDialogService : IBatchRouteDialogService
{
    public IReadOnlyList<BatchRouteEditItem>? Edit(
        IReadOnlyList<RouteInfo> routes,
        IReadOnlyList<NetworkAdapterInfo> adapters)
    {
        var dialog = new BatchRouteManagementWindow(routes, adapters) { Owner = FindOwner() };
        return dialog.ShowDialog() == true ? dialog.SelectedItems : null;
    }

    public void ShowResults(IReadOnlyList<BatchRouteExecutionResult> results)
    {
        new BatchRouteResultWindow(results) { Owner = FindOwner() }.ShowDialog();
    }

    private static Window? FindOwner() =>
        Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive);
}
