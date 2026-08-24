using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class IPv4BindingResetService(
    IPowerShellExecutor powerShellExecutor,
    INetworkAdapterService networkAdapterService) : IIPv4BindingResetService
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MutationTimeout = TimeSpan.FromSeconds(20);
    private const string ComponentId = "ms_tcpip";

    public string GetResetCommand(string adapterName)
    {
        var name = Escape(adapterName);
        return $$"""
            Disable-NetAdapterBinding -Name '{{name}}' -ComponentID {{ComponentId}} -Confirm:$false -ErrorAction Stop | Out-Null
            Enable-NetAdapterBinding -Name '{{name}}' -ComponentID {{ComponentId}} -Confirm:$false -ErrorAction Stop | Out-Null
            """;
    }

    public string GetManualEnableCommand(string adapterName) =>
        $"Enable-NetAdapterBinding -Name '{Escape(adapterName)}' -ComponentID {ComponentId} -Confirm:$false -ErrorAction Stop";

    public async Task<IReadOnlyList<IPv4BindingInfo>> GetBindingsAsync(CancellationToken cancellationToken = default)
    {
        var result = await powerShellExecutor.ExecuteAsync<BindingEnvelope>(BuildReadAllCommand(), ReadTimeout, cancellationToken);
        return result.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) && item.ComponentId == ComponentId)
            .Select(item => new IPv4BindingInfo(item.Name, item.ComponentId, item.Enabled))
            .OrderBy(item => item.AdapterName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public async Task<IPv4BindingInfo> GetBindingAsync(string adapterName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterName);
        BindingCommandResult result;
        try
        {
            result = await powerShellExecutor.ExecuteAsync<BindingCommandResult>(
                BuildReadCommand(adapterName), ReadTimeout, cancellationToken);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"网卡 {adapterName} 不支持 {ComponentId} 绑定操作，或该绑定当前不可读取。", exception);
        }
        if (string.IsNullOrWhiteSpace(result.Name) || result.ComponentId != ComponentId)
        {
            throw new InvalidOperationException($"网卡 {adapterName} 返回了无效的 {ComponentId} 绑定信息。");
        }
        return new IPv4BindingInfo(result.Name, result.ComponentId, result.Enabled);
    }

    public async Task<IPv4BindingResetResult> ResetAsync(
        NetworkAdapterInfo adapter,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        progress?.Report("正在确认网卡和 IPv4 绑定状态…");
        var adapters = await networkAdapterService.GetAdaptersAsync(cancellationToken);
        if (adapters.All(current => current.InterfaceIndex != adapter.InterfaceIndex ||
                                    !current.Name.Equals(adapter.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"网卡 {adapter.Name}（索引 {adapter.InterfaceIndex}）不存在，请刷新后重新选择。");
        }

        var before = await GetBindingAsync(adapter.Name, cancellationToken);
        if (before.Enabled)
        {
            progress?.Report("正在禁用 ms_tcpip 绑定…");
            await ExecuteMutationAsync(BuildDisableCommand(adapter.Name), cancellationToken);
            IPv4BindingInfo? disabled = null;
            try
            {
                disabled = await GetBindingAsync(adapter.Name, CancellationToken.None);
            }
            catch (Exception)
            {
                progress?.Report("暂时无法确认禁用状态，仍将优先执行重新启用以保护网络连接…");
            }
            if (disabled?.Enabled == true)
            {
                throw new InvalidOperationException("Windows 已执行禁用命令，但 ms_tcpip 绑定仍显示为启用。未继续执行重置。");
            }
        }
        else
        {
            progress?.Report("ms_tcpip 当前已禁用，将直接尝试恢复启用…");
        }

        var retried = false;
        try
        {
            progress?.Report("正在重新启用 ms_tcpip 绑定…");
            await ExecuteMutationAsync(BuildEnableCommand(adapter.Name), CancellationToken.None);
        }
        catch (Exception firstException)
        {
            retried = true;
            progress?.Report("首次启用失败，正在自动重试一次…");
            try
            {
                await ExecuteMutationAsync(BuildEnableCommand(adapter.Name), CancellationToken.None);
            }
            catch (Exception retryException)
            {
                throw new InvalidOperationException(
                    $"IPv4 绑定启用失败并且自动重试未成功，当前绑定可能仍处于禁用状态。请以管理员身份执行：{GetManualEnableCommand(adapter.Name)}",
                    new AggregateException(firstException, retryException));
            }
        }

        progress?.Report("正在重新读取 IPv4 绑定和网卡配置进行验证…");
        var after = await GetBindingAsync(adapter.Name, CancellationToken.None);
        if (!after.Enabled)
        {
            throw new InvalidOperationException(
                $"重置命令已执行，但 ms_tcpip 绑定仍为禁用。请以管理员身份执行：{GetManualEnableCommand(adapter.Name)}");
        }
        var verifiedAdapters = await networkAdapterService.GetAdaptersAsync(CancellationToken.None);
        var verified = verifiedAdapters.FirstOrDefault(current =>
            current.InterfaceIndex == adapter.InterfaceIndex &&
            current.Name.Equals(adapter.Name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("IPv4 绑定已启用，但重新读取网卡配置时未找到目标网卡。");
        progress?.Report("IPv4 绑定重置完成并通过验证。");
        return new IPv4BindingResetResult(before, after, verified, retried);
    }

    private async Task ExecuteMutationAsync(string command, CancellationToken cancellationToken)
    {
        var result = await powerShellExecutor.ExecuteAsync<MutationCommandResult>(command, MutationTimeout, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Windows 未确认 IPv4 绑定操作成功。");
        }
    }

    private static string BuildReadCommand(string adapterName) => $$"""
        $binding = Get-NetAdapterBinding -Name '{{Escape(adapterName)}}' -ComponentID {{ComponentId}} -ErrorAction Stop
        [pscustomobject]@{ Name = [string]$binding.Name; ComponentId = [string]$binding.ComponentID; Enabled = [bool]$binding.Enabled }
        """;

    private static string BuildReadAllCommand() => $$"""
        $items = @(Get-NetAdapterBinding -ComponentID {{ComponentId}} -ErrorAction Stop | ForEach-Object {
            [pscustomobject]@{ Name = [string]$_.Name; ComponentId = [string]$_.ComponentID; Enabled = [bool]$_.Enabled }
        })
        [pscustomobject]@{ Items = $items }
        """;

    private static string BuildDisableCommand(string adapterName) => $$"""
        Disable-NetAdapterBinding -Name '{{Escape(adapterName)}}' -ComponentID {{ComponentId}} -Confirm:$false -ErrorAction Stop | Out-Null
        [pscustomobject]@{ Succeeded = $true }
        """;

    private static string BuildEnableCommand(string adapterName) => $$"""
        Enable-NetAdapterBinding -Name '{{Escape(adapterName)}}' -ComponentID {{ComponentId}} -Confirm:$false -ErrorAction Stop | Out-Null
        [pscustomobject]@{ Succeeded = $true }
        """;

    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private sealed class BindingCommandResult
    {
        public string Name { get; init; } = string.Empty;
        public string ComponentId { get; init; } = string.Empty;
        public bool Enabled { get; init; }
    }

    private sealed class BindingEnvelope
    {
        public IReadOnlyList<BindingCommandResult> Items { get; init; } = [];
    }

    private sealed class MutationCommandResult
    {
        public bool Succeeded { get; init; }
    }
}
