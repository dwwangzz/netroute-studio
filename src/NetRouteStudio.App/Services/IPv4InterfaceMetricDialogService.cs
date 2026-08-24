using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace NetRouteStudio.App.Services;

public sealed class IPv4InterfaceMetricDialogService(IServiceProvider serviceProvider)
    : IIPv4InterfaceMetricDialogService
{
    public void Show()
    {
        var window = serviceProvider.GetRequiredService<IPv4InterfaceMetricWindow>();
        window.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive);
        window.ShowDialog();
    }
}
