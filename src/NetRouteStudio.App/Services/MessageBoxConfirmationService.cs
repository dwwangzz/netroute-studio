using System.Windows;

namespace NetRouteStudio.App.Services;

public sealed class MessageBoxConfirmationService : IConfirmationService
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
}
