using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.ViewModels;

public sealed partial class IPv6ResetViewModel(
    INetworkAdapterService networkAdapterService,
    IIPv4BindingResetService ipv4ResetService,
    IIPv6ResetService resetService,
    IConfirmationService confirmationService,
    ILogger<IPv6ResetViewModel> logger) : ObservableObject
{
    private IReadOnlyDictionary<string, IPv4BindingInfo> _ipv4Bindings = new Dictionary<string, IPv4BindingInfo>();
    private IReadOnlyDictionary<string, IPv6BindingInfo> _ipv6Bindings = new Dictionary<string, IPv6BindingInfo>();

    [ObservableProperty] private NetworkAdapterInfo? _selectedAdapter;
    [ObservableProperty] private string _bindingStatus = "尚未读取";
    [ObservableProperty] private string _ipv4BindingStatus = "尚未读取";
    [ObservableProperty] private string _statusMessage = "请选择一张网卡执行 IPv6 绑定重置。";
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private string _manualRecoveryCommand = string.Empty;
    [ObservableProperty] private string _ipv4ManualRecoveryCommand = string.Empty;
    [ObservableProperty] private string _copyStatusMessage = string.Empty;
    [ObservableProperty] private bool _isIPv4Supported;
    [ObservableProperty] private bool _isIPv6Supported;
    [ObservableProperty] private bool _isLoading;

    public ObservableCollection<NetworkAdapterInfo> Adapters { get; } = [];

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
            var adaptersTask = networkAdapterService.GetAdaptersAsync();
            var ipv4BindingsTask = ipv4ResetService.GetBindingsAsync();
            var bindingsTask = resetService.GetBindingsAsync();
            await Task.WhenAll(adaptersTask, ipv4BindingsTask, bindingsTask);
            var adapters = await adaptersTask;
            var ipv4Bindings = await ipv4BindingsTask;
            var bindings = await bindingsTask;
            _ipv4Bindings = ipv4Bindings.ToDictionary(
                binding => binding.AdapterName,
                StringComparer.OrdinalIgnoreCase);
            _ipv6Bindings = bindings.ToDictionary(
                binding => binding.AdapterName,
                StringComparer.OrdinalIgnoreCase);
            Adapters.Clear();
            foreach (var adapter in adapters
                         .Where(adapter => _ipv4Bindings.ContainsKey(adapter.Name) || _ipv6Bindings.ContainsKey(adapter.Name))
                         .OrderBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Adapters.Add(adapter);
            }
            SelectedAdapter = Adapters.FirstOrDefault();
            if (SelectedAdapter is null)
            {
                BindingStatus = "无可重置网卡";
                StatusMessage = "没有找到支持 ms_tcpip6 绑定操作的网络适配器。";
            }
            else
            {
                UpdateSelectedBindingState(SelectedAdapter);
                StatusMessage = $"已读取 {Adapters.Count} 张支持 IPv4 或 IPv6 绑定重置的网卡。";
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "读取 IPv6 重置网卡数据失败");
            ErrorMessage = exception.Message;
            StatusMessage = "网卡或 IPv6 绑定状态读取失败";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ReadBindingAsync()
    {
        if (SelectedAdapter is null || !IsIPv6Supported)
        {
            ErrorMessage = "当前网卡不支持 ms_tcpip6（IPv6）绑定操作。";
            return;
        }
        await LoadBindingAsync(SelectedAdapter);
    }

    [RelayCommand]
    private async Task ReadIPv4BindingAsync()
    {
        if (SelectedAdapter is null || !IsIPv4Supported)
        {
            ErrorMessage = "当前网卡不支持 ms_tcpip（IPv4）绑定操作。";
            return;
        }
        try
        {
            var binding = await ipv4ResetService.GetBindingAsync(SelectedAdapter.Name);
            Ipv4BindingStatus = binding.StatusDisplay;
            ErrorMessage = string.Empty;
            StatusMessage = $"已读取 {SelectedAdapter.Name} 的 ms_tcpip 绑定状态。";
        }
        catch (Exception exception)
        {
            Ipv4BindingStatus = "读取失败";
            ErrorMessage = exception.Message;
        }
    }

    [RelayCommand]
    private async Task ResetIPv4Async()
    {
        if (SelectedAdapter is null || !IsIPv4Supported)
        {
            ErrorMessage = "请选择支持 ms_tcpip（IPv4）绑定操作的网卡。";
            return;
        }

        var adapterToReset = SelectedAdapter;
        try
        {
            var binding = await ipv4ResetService.GetBindingAsync(adapterToReset.Name);
            Ipv4BindingStatus = binding.StatusDisplay;
            if (!confirmationService.Confirm(new RouteConfirmationRequest(
                    "确认重置 IPv4 绑定",
                    "重置单张网卡的 IPv4（ms_tcpip）绑定",
                    BuildIPv4ConfirmationFields(adapterToReset, binding),
                    ipv4ResetService.GetResetCommand(adapterToReset.Name).Trim())))
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
            var result = await ipv4ResetService.ResetAsync(
                adapterToReset,
                new Progress<string>(message => StatusMessage = message));
            Ipv4BindingStatus = result.After.StatusDisplay;
            ReplaceAdapter(adapterToReset, result.VerifiedAdapter);
            StatusMessage = result.EnableRetried
                ? "IPv4 绑定重置成功；首次启用失败后自动重试成功。"
                : "IPv4 绑定重置成功并通过实际状态验证。";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "IPv4 绑定重置失败：{AdapterName}", adapterToReset.Name);
            ErrorMessage = exception.Message;
            Ipv4ManualRecoveryCommand = ipv4ResetService.GetManualEnableCommand(adapterToReset.Name);
            StatusMessage = "IPv4 绑定重置失败，请检查状态并按需执行手工恢复命令。";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (SelectedAdapter is null || !IsIPv6Supported)
        {
            ErrorMessage = "请选择支持 ms_tcpip6（IPv6）绑定操作的网卡。";
            return;
        }

        IPv6BindingInfo binding;
        try
        {
            binding = await resetService.GetBindingAsync(SelectedAdapter.Name);
            BindingStatus = binding.StatusDisplay;
            var fields = BuildConfirmationFields(SelectedAdapter, binding);
            if (!confirmationService.Confirm(new RouteConfirmationRequest(
                    "确认重置 IPv6 绑定",
                    "重置单张网卡的 IPv6（ms_tcpip6）绑定",
                    fields,
                    resetService.GetResetCommand(SelectedAdapter.Name).Trim())))
            {
                return;
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            return;
        }

        var adapterToReset = SelectedAdapter;
        IsLoading = true;
        ErrorMessage = string.Empty;
        ManualRecoveryCommand = string.Empty;
        try
        {
            var progress = new Progress<string>(message => StatusMessage = message);
            var result = await resetService.ResetAsync(adapterToReset, progress);
            BindingStatus = result.After.StatusDisplay;
            ReplaceAdapter(adapterToReset, result.VerifiedAdapter);
            StatusMessage = result.EnableRetried
                ? "IPv6 绑定重置成功；首次启用失败后自动重试成功。"
                : "IPv6 绑定重置成功并通过实际状态验证。";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "IPv6 绑定重置失败：{AdapterName}", adapterToReset.Name);
            ErrorMessage = exception.Message;
            ManualRecoveryCommand = resetService.GetManualEnableCommand(adapterToReset.Name);
            StatusMessage = "IPv6 绑定重置失败，请检查绑定状态并按需执行手工恢复命令。";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedAdapterChanged(NetworkAdapterInfo? value)
    {
        UpdateSelectedBindingState(value);
        ErrorMessage = string.Empty;
        CopyStatusMessage = string.Empty;
    }

    private void UpdateSelectedBindingState(NetworkAdapterInfo? adapter)
    {
        IPv4BindingInfo? ipv4Binding = null;
        IPv6BindingInfo? ipv6Binding = null;
        IsIPv4Supported = adapter is not null && _ipv4Bindings.TryGetValue(adapter.Name, out ipv4Binding);
        IsIPv6Supported = adapter is not null && _ipv6Bindings.TryGetValue(adapter.Name, out ipv6Binding);
        Ipv4BindingStatus = ipv4Binding?.StatusDisplay ?? "不支持 ms_tcpip";
        BindingStatus = ipv6Binding?.StatusDisplay ?? "不支持 ms_tcpip6";
        Ipv4ManualRecoveryCommand = IsIPv4Supported
            ? ipv4ResetService.GetManualEnableCommand(adapter!.Name)
            : string.Empty;
        ManualRecoveryCommand = IsIPv6Supported
            ? resetService.GetManualEnableCommand(adapter!.Name)
            : string.Empty;
    }

    private void ReplaceAdapter(NetworkAdapterInfo before, NetworkAdapterInfo after)
    {
        var index = Adapters.IndexOf(before);
        if (index >= 0)
        {
            Adapters[index] = after;
        }
        SelectedAdapter = after;
    }

    private async Task LoadBindingAsync(NetworkAdapterInfo adapter)
    {
        try
        {
            var binding = await resetService.GetBindingAsync(adapter.Name);
            if (SelectedAdapter == adapter)
            {
                BindingStatus = binding.StatusDisplay;
                StatusMessage = $"已读取 {adapter.Name} 的 ms_tcpip6 绑定状态。";
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "读取 IPv6 绑定状态失败：{AdapterName}", adapter.Name);
            BindingStatus = "读取失败";
            ErrorMessage = exception.Message;
        }
    }

    private static IReadOnlyList<RouteConfirmationField> BuildConfirmationFields(
        NetworkAdapterInfo adapter,
        IPv6BindingInfo binding) =>
    [
        new("风险", "当前网络连接", "IPv6 连接可能短暂中断"),
        new("网卡名称", adapter.Name, adapter.Name),
        new("网卡描述", adapter.InterfaceDescription, adapter.InterfaceDescription),
        new("接口索引", adapter.InterfaceIndex.ToString(), adapter.InterfaceIndex.ToString()),
        new("网卡状态", adapter.Status, adapter.Status),
        new("IPv6 地址", adapter.IPv6Display, "重置后由 Windows 重新配置"),
        new("网关", adapter.GatewayDisplay, "不主动修改"),
        new("DNS", adapter.DnsDisplay, "不主动修改"),
        new("ms_tcpip6 绑定", binding.StatusDisplay, "禁用后重新启用")
    ];

    private static IReadOnlyList<RouteConfirmationField> BuildIPv4ConfirmationFields(
        NetworkAdapterInfo adapter,
        IPv4BindingInfo binding) =>
    [
        new("风险", "当前网络连接", "IPv4 连接可能短暂中断"),
        new("网卡名称", adapter.Name, adapter.Name),
        new("网卡描述", adapter.InterfaceDescription, adapter.InterfaceDescription),
        new("接口索引", adapter.InterfaceIndex.ToString(), adapter.InterfaceIndex.ToString()),
        new("网卡状态", adapter.Status, adapter.Status),
        new("IPv4 地址", adapter.IPv4Display, "重置后由 Windows 重新配置"),
        new("网关", adapter.GatewayDisplay, "不主动修改"),
        new("DNS", adapter.DnsDisplay, "不主动修改"),
        new("ms_tcpip 绑定", binding.StatusDisplay, "禁用后重新启用")
    ];
}
