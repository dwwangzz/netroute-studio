using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace NetRouteStudio.App.Services;

public sealed class NetworkTestDialogService(IServiceProvider serviceProvider) : INetworkTestDialogService
{
    public void Show()
    {
        var window = serviceProvider.GetRequiredService<NetworkTestWindow>();
        window.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive);
        window.ShowDialog();
    }
}
