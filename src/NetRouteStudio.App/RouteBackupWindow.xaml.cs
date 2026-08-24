using System.Windows;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App;

public partial class RouteBackupWindow : Window
{
    public RouteBackupWindow(RouteBackupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
