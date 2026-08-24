using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;
namespace NetRouteStudio.App.ViewModels;
public sealed partial class ControlledCommandViewModel(IControlledCommandService service) : ObservableObject
{
    private CancellationTokenSource? _cancellation;
    [ObservableProperty] private string _commandText = "ping -n 4 127.0.0.1";
    [ObservableProperty] private string _output = string.Empty;
    [ObservableProperty] private string _statusMessage = "仅允许界面列出的白名单网络诊断命令。";
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isRunning;
    public ObservableCollection<ControlledCommandResult> History { get; } = [];
    [RelayCommand] private async Task ExecuteAsync()
    {
        if (IsRunning) return;
        IsRunning = true; ErrorMessage = string.Empty; Output = string.Empty; StatusMessage = "正在执行…"; _cancellation = new();
        try { var result = await service.ExecuteAsync(CommandText, _cancellation.Token); History.Insert(0, result); Output = result.Output; StatusMessage = $"执行完成：{result.StatusDisplay}，耗时 {result.Duration.TotalSeconds:F1} 秒。"; }
        catch (Exception exception) { ErrorMessage = exception.Message; StatusMessage = "命令未执行或执行失败。"; }
        finally { _cancellation?.Dispose(); _cancellation = null; IsRunning = false; }
    }
    [RelayCommand] private void Cancel() => _cancellation?.Cancel();
    partial void OnSelectedHistoryChanged(ControlledCommandResult? value) { if (value is not null) Output = value.Output; }
    [ObservableProperty] private ControlledCommandResult? _selectedHistory;
}
