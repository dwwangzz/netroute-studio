using System.Windows;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App;

public partial class MainWindow : Window
{
    public MainWindow(RouteMatchViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
