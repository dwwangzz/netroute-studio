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

    private async void OnCopyIPv4RecoveryCommand(object sender, RoutedEventArgs e)
    {
        await CopyRecoveryCommandAsync(_viewModel.Ipv4ManualRecoveryCommand, CopyIPv4RecoveryButton);
    }

    private async void OnCopyIPv6RecoveryCommand(object sender, RoutedEventArgs e)
    {
        await CopyRecoveryCommandAsync(_viewModel.ManualRecoveryCommand, CopyIPv6RecoveryButton);
    }

    private async Task CopyRecoveryCommandAsync(string command, System.Windows.Controls.Button button)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        try
        {
            Clipboard.SetText(command);
            _viewModel.ErrorMessage = string.Empty;
            _viewModel.CopyStatusMessage = "手工恢复命令已复制到剪贴板。";
            button.Content = "已复制";
            await Task.Delay(TimeSpan.FromSeconds(2));
            button.Content = "复制手工恢复命令";
            _viewModel.CopyStatusMessage = string.Empty;
        }
        catch (Exception exception)
        {
            button.Content = "复制手工恢复命令";
            _viewModel.CopyStatusMessage = string.Empty;
            _viewModel.ErrorMessage = $"复制失败：{exception.Message}";
        }
    }
}
