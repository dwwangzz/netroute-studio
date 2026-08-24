using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.ViewModels;

public sealed partial class NetworkTestViewModel(INetworkTestService networkTestService) : ObservableObject
{
    private CancellationTokenSource? _testCancellation;
    [ObservableProperty] private string _target = string.Empty;
    [ObservableProperty] private string _statusMessage = "请输入 IP 地址或域名开始网络测试。";
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isRunning;
    public ObservableCollection<string> ResolvedAddresses { get; } = [];
    public ObservableCollection<NetworkPingResult> PingResults { get; } = [];
    public ObservableCollection<TraceRouteHop> TraceHops { get; } = [];
    public ObservableCollection<RouteMatchResult> RouteMatches { get; } = [];

    [RelayCommand]
    private async Task StartAsync()
    {
        if (IsRunning) return;
        ClearResults();
        IsRunning = true;
        ErrorMessage = string.Empty;
        _testCancellation = new CancellationTokenSource();
        try
        {
            var result = await networkTestService.TestAsync(Target, new Progress<string>(message => StatusMessage = message), _testCancellation.Token);
            foreach (var item in result.ResolvedAddresses) ResolvedAddresses.Add(item);
            foreach (var item in result.PingResults) PingResults.Add(item);
            foreach (var item in result.TraceHops) TraceHops.Add(item);
            foreach (var item in result.RouteMatches) RouteMatches.Add(item);
            Summary = result.Summary;
            StatusMessage = "网络测试完成。";
        }
        catch (OperationCanceledException) { StatusMessage = "网络测试已取消。"; }
        catch (Exception exception) { ErrorMessage = exception.Message; StatusMessage = "网络测试失败。"; }
        finally { _testCancellation?.Dispose(); _testCancellation = null; IsRunning = false; }
    }

    [RelayCommand]
    private void Cancel() => _testCancellation?.Cancel();

    private void ClearResults()
    {
        ResolvedAddresses.Clear(); PingResults.Clear(); TraceHops.Clear(); RouteMatches.Clear(); Summary = string.Empty;
    }
}
