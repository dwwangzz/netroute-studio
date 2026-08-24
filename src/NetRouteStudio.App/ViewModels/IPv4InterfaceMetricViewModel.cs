using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.ViewModels;

public sealed partial class IPv4InterfaceMetricViewModel(
    INetworkAdapterService networkAdapterService,
    IIPv4InterfaceMetricService metricService,
    IConfirmationService confirmationService,
    ILogger<IPv4InterfaceMetricViewModel> logger) : ObservableObject
{
    [ObservableProperty] private NetworkAdapterInfo? _selectedAdapter;
    [ObservableProperty] private bool _automaticMetric = true;
    [ObservableProperty] private string _manualMetric = "25";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "等待读取网卡接口跃点";
    [ObservableProperty] private string _errorMessage = string.Empty;

    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = [];

    public bool IsManualMetricEnabled => !AutomaticMetric;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var adapters = await networkAdapterService.GetAdaptersAsync();
            Adapters.Clear();
            foreach (var adapter in adapters.OrderBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Adapters.Add(adapter);
            }

            SelectedAdapter = Adapters.FirstOrDefault();
            StatusMessage = $"已读取 {Adapters.Count} 个网络接口的 IPv4 跃点设置。";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "读取 IPv4 接口跃点失败");
            ErrorMessage = exception.Message;
            StatusMessage = "IPv4 接口跃点读取失败";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (SelectedAdapter is null)
        {
            ErrorMessage = "请先选择要修改的网络接口。";
            return;
        }

        IPv4InterfaceMetricRequest request;
        try
        {
            int? metric = null;
            if (!AutomaticMetric)
            {
                if (!int.TryParse(ManualMetric, out var parsedMetric))
                {
                    throw new ArgumentException("手动 IPv4 接口 Metric 必须是整数。");
                }
                metric = parsedMetric;
            }

            request = new IPv4InterfaceMetricRequest(SelectedAdapter.InterfaceIndex, AutomaticMetric, metric);
            var command = metricService.GetUpdateCommand(request);
            var fields = BuildConfirmationFields(SelectedAdapter, request);
            if (!confirmationService.Confirm(new RouteConfirmationRequest(
                    "确认修改 IPv4 接口跃点",
                    "修改 IPv4 接口跃点",
                    fields,
                    command.Trim())))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            return;
        }

        IsLoading = true;
        ErrorMessage = string.Empty;
        try
        {
            var result = await metricService.UpdateAsync(request);
            var index = Adapters.IndexOf(SelectedAdapter);
            if (index >= 0)
            {
                Adapters[index] = result.VerifiedAdapter;
            }
            SelectedAdapter = result.VerifiedAdapter;
            StatusMessage = result.Message;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "修改 IPv4 接口跃点失败：{InterfaceIndex}", request.InterfaceIndex);
            ErrorMessage = exception.Message;
            StatusMessage = "IPv4 接口跃点修改失败";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedAdapterChanged(NetworkAdapterInfo? value)
    {
        if (value is null)
        {
            return;
        }

        AutomaticMetric = value.IPv4AutomaticMetric ?? true;
        ManualMetric = (value.IPv4InterfaceMetric ?? 25).ToString();
        ErrorMessage = string.Empty;
    }

    partial void OnAutomaticMetricChanged(bool value) =>
        OnPropertyChanged(nameof(IsManualMetricEnabled));

    private static IReadOnlyList<RouteConfirmationField> BuildConfirmationFields(
        NetworkAdapterInfo adapter,
        IPv4InterfaceMetricRequest request)
    {
        var beforeMode = adapter.IPv4AutomaticMetric == true ? "自动" : "手动";
        var afterMode = request.AutomaticMetric ? "自动" : "手动";
        return
        [
            new("网卡名称", adapter.Name, adapter.Name),
            new("网卡描述", adapter.InterfaceDescription, adapter.InterfaceDescription),
            new("接口索引", adapter.InterfaceIndex.ToString(), adapter.InterfaceIndex.ToString()),
            new("状态", adapter.Status, adapter.Status),
            new("IPv4 地址", adapter.IPv4Display, adapter.IPv4Display),
            new("跃点模式", beforeMode, afterMode),
            new("IPv4 接口 Metric", adapter.IPv4InterfaceMetric?.ToString() ?? "—",
                request.AutomaticMetric ? "由 Windows 自动计算" : request.InterfaceMetric!.Value.ToString())
        ];
    }
}
