using System.Windows;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App;

public partial class NetworkTestWindow : Window
{
    public NetworkTestWindow(NetworkTestViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
