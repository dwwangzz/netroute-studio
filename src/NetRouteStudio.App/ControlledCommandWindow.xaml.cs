using System.Windows;
using NetRouteStudio.App.ViewModels;
using System.Windows.Controls;
namespace NetRouteStudio.App;
public partial class ControlledCommandWindow : Window
{
    public ControlledCommandWindow(ControlledCommandViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    private void OnWhitelistUnchecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ControlledCommandViewModel vm) return;
        var result = MessageBox.Show("关闭白名单后可以启动更多系统 PATH 中的程序。仍会禁止命令解释器、脚本宿主、路径和连接/重定向字符。是否继续？", "关闭命令白名单", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) { vm.IsWhitelistEnabled = true; return; }
        vm.StatusMessage = "命令白名单已关闭，本次窗口关闭后将自动恢复开启。";
    }
    private void OnWhitelistChecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is ControlledCommandViewModel vm) vm.StatusMessage = "命令白名单已启用。";
    }
    private void OnOutputTextChanged(object sender, TextChangedEventArgs e) => OutputTextBox.ScrollToEnd();
    private void OnCopyCommand(object sender, RoutedEventArgs e) { if (DataContext is ControlledCommandViewModel vm && !string.IsNullOrWhiteSpace(vm.CommandText)) { Clipboard.SetText(vm.CommandText); vm.StatusMessage = "命令已复制到剪贴板。"; } }
    private void OnCopy(object sender, RoutedEventArgs e) { if (DataContext is ControlledCommandViewModel vm && !string.IsNullOrWhiteSpace(vm.Output)) { Clipboard.SetText(vm.Output); vm.StatusMessage = "输出已复制到剪贴板。"; } }
}
