using System.Windows;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App;

public partial class IPv6ResetWindow : Window
{
    private readonly IPv6ResetViewModel _viewModel;

    public IPv6ResetWindow(IPv6ResetViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.RefreshAsync();
    }

    private void OnCopyRecoveryCommand(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.ManualRecoveryCommand))
        {
            Clipboard.SetText(_viewModel.ManualRecoveryCommand);
        }
    }
}
