using System.Windows;
using System.Windows.Controls;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App;

public partial class RouteBackupWindow : Window
{
    public RouteBackupWindow(RouteBackupViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnRestoreSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: RouteRestoreDiffItem item } checkBox)
        {
            item.IsSelected = checkBox.IsChecked == true;
        }
    }
}
