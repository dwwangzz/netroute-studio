using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class IPv4InterfaceMetricService(
    IPowerShellExecutor powerShellExecutor,
    INetworkAdapterService networkAdapterService) : IIPv4InterfaceMetricService
{
    private static readonly TimeSpan MutationTimeout = TimeSpan.FromSeconds(20);

    public string GetUpdateCommand(IPv4InterfaceMetricRequest request) => BuildCommand(Validate(request));

    public async Task<InterfaceMetricMutationResult> UpdateAsync(
        IPv4InterfaceMetricRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = Validate(request);
        var before = await networkAdapterService.GetAdaptersAsync(cancellationToken);
        if (before.All(adapter => adapter.InterfaceIndex != normalized.InterfaceIndex))
        {
            throw new ArgumentException($"接口索引 {normalized.InterfaceIndex} 不存在，请刷新网卡列表后重新选择。");
        }

        var result = await powerShellExecutor.ExecuteAsync<MutationCommandResult>(
            BuildCommand(normalized), MutationTimeout, cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Windows 未确认 IPv4 接口跃点修改成功。");
        }

        var after = await networkAdapterService.GetAdaptersAsync(cancellationToken);
        var verified = after.FirstOrDefault(adapter =>
            adapter.InterfaceIndex == normalized.InterfaceIndex &&
            adapter.IPv4AutomaticMetric == normalized.AutomaticMetric &&
            (normalized.AutomaticMetric || adapter.IPv4InterfaceMetric == normalized.InterfaceMetric));
        if (verified is null)
        {
            throw new InvalidOperationException("命令已执行，但重新读取 Windows 网卡配置后未找到匹配的 IPv4 接口跃点设置。");
        }

        return new InterfaceMetricMutationResult("IPv4 接口跃点已修改并通过实际网卡配置验证。", verified);
    }

    private static IPv4InterfaceMetricRequest Validate(IPv4InterfaceMetricRequest request)
    {
        if (request.InterfaceIndex <= 0)
        {
            throw new ArgumentException("接口索引必须是正整数。");
        }

        if (!request.AutomaticMetric && request.InterfaceMetric is not (>= 1 and <= 9999))
        {
            throw new ArgumentException("手动 IPv4 接口 Metric 必须是 1 到 9999 之间的整数。");
        }

        return request.AutomaticMetric ? request with { InterfaceMetric = null } : request;
    }

    private static string BuildCommand(IPv4InterfaceMetricRequest request)
    {
        var metricArgument = request.AutomaticMetric ? string.Empty : $" -InterfaceMetric {request.InterfaceMetric}";
        var automaticMetric = request.AutomaticMetric ? "Enabled" : "Disabled";
        return $$"""
            Set-NetIPInterface -InterfaceIndex {{request.InterfaceIndex}} -AddressFamily IPv4 -AutomaticMetric {{automaticMetric}}{{metricArgument}} -ErrorAction Stop | Out-Null
            [pscustomobject]@{ Succeeded = $true }
            """;
    }

    private sealed class MutationCommandResult
    {
        public bool Succeeded { get; init; }
    }
}
