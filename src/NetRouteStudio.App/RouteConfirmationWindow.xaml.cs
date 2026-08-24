using System.Windows;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App;

public partial class RouteConfirmationWindow : Window
{
    private readonly RouteConfirmationRequest _request;

    public RouteConfirmationWindow(RouteConfirmationRequest request)
    {
        InitializeComponent();
        _request = request;
        Title = request.Title;
        DataContext = request;
    }

    private void OnCopyCommand(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_request.Command);
        CopyButton.Content = "已复制";
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
