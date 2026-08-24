using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace NetRouteStudio.App.Services;

public sealed class RouteBackupDialogService(IServiceProvider serviceProvider) : IRouteBackupDialogService
{
    public void Show()
    {
        var window = serviceProvider.GetRequiredService<RouteBackupWindow>();
        window.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive);
        window.ShowDialog();
    }
}
