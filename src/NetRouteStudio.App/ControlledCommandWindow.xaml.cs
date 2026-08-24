using System.Windows;
using NetRouteStudio.App.ViewModels;
using System.Windows.Controls;
namespace NetRouteStudio.App;
public partial class ControlledCommandWindow : Window
{
    public ControlledCommandWindow(ControlledCommandViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    private void OnOutputTextChanged(object sender, TextChangedEventArgs e) => OutputTextBox.ScrollToEnd();
    private void OnCopyCommand(object sender, RoutedEventArgs e) { if (DataContext is ControlledCommandViewModel vm && !string.IsNullOrWhiteSpace(vm.CommandText)) { Clipboard.SetText(vm.CommandText); vm.StatusMessage = "命令已复制到剪贴板。"; } }
    private void OnCopy(object sender, RoutedEventArgs e) { if (DataContext is ControlledCommandViewModel vm && !string.IsNullOrWhiteSpace(vm.Output)) { Clipboard.SetText(vm.Output); vm.StatusMessage = "输出已复制到剪贴板。"; } }
}
