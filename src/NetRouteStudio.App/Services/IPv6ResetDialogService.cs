using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace NetRouteStudio.App.Services;

public sealed class IPv6ResetDialogService(IServiceProvider serviceProvider) : IIPv6ResetDialogService
{
    public void Show()
    {
        var window = serviceProvider.GetRequiredService<IPv6ResetWindow>();
        window.Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.IsActive);
        window.ShowDialog();
    }
}
