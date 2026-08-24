using System.Windows;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class RouteConfirmationService : IConfirmationService
{
    public bool Confirm(RouteConfirmationRequest request)
    {
        var dialog = new RouteConfirmationWindow(request)
        {
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(window => window.IsActive)
        };
        return dialog.ShowDialog() == true;
    }
}
