using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class IPv4RouteManagementService(
    IPowerShellExecutor powerShellExecutor,
    IRouteTableService routeTableService,
    INetworkAdapterService networkAdapterService) : IIPv4RouteManagementService
{
    private static readonly TimeSpan MutationTimeout = TimeSpan.FromSeconds(20);

    public async Task<RouteMutationResult> CreateAsync(
        IPv4RouteRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = IPv4RouteValidator.ValidateAndNormalize(request);
        await EnsureInterfaceExistsAsync(normalized.InterfaceIndex, cancellationToken);
        await ExecuteMutationAsync(BuildCreateCommand(normalized), cancellationToken);
        var verifiedRoute = await FindVerifiedRouteAsync(normalized, cancellationToken);
        if (verifiedRoute is null)
        {
            throw new InvalidOperationException("命令已执行，但重新读取 Windows 路由表后未找到新增路由。");
        }

        return new RouteMutationResult("IPv4 路由已新增并通过实际路由表验证。", verifiedRoute);
    }

    public async Task<RouteMutationResult> UpdateAsync(
        RouteInfo existingRoute,
        IPv4RouteRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateOperableRoute(existingRoute);
        var normalized = IPv4RouteValidator.ValidateAndNormalize(request);
        await EnsureInterfaceExistsAsync(normalized.InterfaceIndex, cancellationToken);
        await ExecuteMutationAsync(BuildUpdateCommand(existingRoute, normalized), cancellationToken);
        var verifiedRoute = await FindVerifiedRouteAsync(normalized, cancellationToken);
        if (verifiedRoute is null)
        {
            throw new InvalidOperationException("命令已执行，但重新读取 Windows 路由表后未找到修改后的路由。");
        }

        return new RouteMutationResult("IPv4 路由已修改并通过实际路由表验证。", verifiedRoute);
    }

    public async Task<RouteMutationResult> DeleteAsync(
        RouteInfo route,
        CancellationToken cancellationToken = default)
    {
        ValidateOperableRoute(route);
        await ExecuteMutationAsync(BuildDeleteCommand(route), cancellationToken);
        var routes = await routeTableService.GetRoutesAsync(cancellationToken);
        if (routes.Any(current => IsSameRoute(current, route)))
        {
            throw new InvalidOperationException("命令已执行，但重新读取 Windows 路由表后该路由仍然存在。");
        }

        return new RouteMutationResult("IPv4 路由已删除并通过实际路由表验证。", null);
    }

    private async Task ExecuteMutationAsync(string command, CancellationToken cancellationToken)
    {
        var result = await powerShellExecutor.ExecuteAsync<MutationCommandResult>(
            command,
            MutationTimeout,
            cancellationToken);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Windows 未确认路由操作成功。");
        }
    }

    private async Task<RouteInfo?> FindVerifiedRouteAsync(
        IPv4RouteRequest request,
        CancellationToken cancellationToken)
    {
        var routes = await routeTableService.GetRoutesAsync(cancellationToken);
        return routes.FirstOrDefault(route =>
            route.AddressFamily == RouteAddressFamily.IPv4 &&
            route.DestinationPrefix == request.DestinationPrefix &&
            route.NextHop == request.NextHop &&
            (route.InterfaceIndex == request.InterfaceIndex ||
             (request.IsPersistent && !route.IsActive && route.InterfaceIndex == 0)) &&
            route.RouteMetric == request.RouteMetric &&
            route.IsPersistent == request.IsPersistent);
    }

    private static string BuildCreateCommand(IPv4RouteRequest request)
    {
        return $$"""
            {{BuildAddStatement(request)}}
            [pscustomobject]@{ Succeeded = $true }
            """;
    }

    private static string BuildUpdateCommand(RouteInfo existing, IPv4RouteRequest request)
    {
        return $$"""
            {{BuildRemoveStatement(existing)}}
            try {
                {{BuildAddStatement(request)}}
            }
            catch {
                # 恢复原路由
                {{BuildRestoreStatement(existing)}}
                throw
            }
            [pscustomobject]@{ Succeeded = $true }
            """;
    }

    private static string BuildDeleteCommand(RouteInfo route)
    {
        return $$"""
            {{BuildRemoveStatement(route)}}
            [pscustomobject]@{ Succeeded = $true }
            """;
    }

    private static string BuildAddStatement(IPv4RouteRequest request) => request.IsPersistent
        ? $$"""
          & netsh.exe interface ipv4 add route "prefix={{request.DestinationPrefix}}" "interface={{request.InterfaceIndex}}" {{BuildNetshNextHopArgument(request.NextHop)}} "metric={{request.RouteMetric}}" store=persistent | Out-Null
          if ($LASTEXITCODE -ne 0) { throw "netsh 新增永久 IPv4 路由失败（退出码 $LASTEXITCODE）。" }
          """
        : $"New-NetRoute -DestinationPrefix '{request.DestinationPrefix}' -InterfaceIndex {request.InterfaceIndex} -NextHop '{request.NextHop}' -RouteMetric {request.RouteMetric} -PolicyStore ActiveStore -ErrorAction Stop | Out-Null";

    private static string BuildRemoveStatement(RouteInfo route) => route.IsPersistent
        ? $$"""
          & netsh.exe interface ipv4 delete route "prefix={{route.DestinationPrefix}}" "interface={{route.InterfaceIndex}}" {{BuildNetshNextHopArgument(route.NextHop)}} store=persistent | Out-Null
          if ($LASTEXITCODE -ne 0) { throw "netsh 删除永久 IPv4 路由失败（退出码 $LASTEXITCODE）。" }
          """
        : $"Remove-NetRoute -DestinationPrefix '{route.DestinationPrefix}' -InterfaceIndex {route.InterfaceIndex} -NextHop '{route.NextHop}' -PolicyStore ActiveStore -Confirm:$false -ErrorAction Stop | Out-Null";

    private static string BuildRestoreStatement(RouteInfo route) => route.IsPersistent
        ? $"& netsh.exe interface ipv4 add route \"prefix={route.DestinationPrefix}\" \"interface={route.InterfaceIndex}\" {BuildNetshNextHopArgument(route.NextHop)} \"metric={route.RouteMetric}\" store=persistent | Out-Null"
        : $"New-NetRoute -DestinationPrefix '{route.DestinationPrefix}' -InterfaceIndex {route.InterfaceIndex} -NextHop '{route.NextHop}' -RouteMetric {route.RouteMetric} -PolicyStore ActiveStore -ErrorAction SilentlyContinue | Out-Null";

    private static string BuildNetshNextHopArgument(string nextHop) =>
        nextHop == "0.0.0.0" ? string.Empty : $"\"nexthop={nextHop}\"";

    private async Task EnsureInterfaceExistsAsync(int interfaceIndex, CancellationToken cancellationToken)
    {
        var adapters = await networkAdapterService.GetAdaptersAsync(cancellationToken);
        if (adapters.All(adapter => adapter.InterfaceIndex != interfaceIndex))
        {
            throw new ArgumentException($"接口索引 {interfaceIndex} 不存在，请刷新网卡列表后重新选择。");
        }
    }

    private static void ValidateOperableRoute(RouteInfo route)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (route.AddressFamily != RouteAddressFamily.IPv4)
        {
            throw new ArgumentException("当前模块只允许管理 IPv4 路由。");
        }

    }

    private static bool IsSameRoute(RouteInfo left, RouteInfo right) =>
        left.AddressFamily == right.AddressFamily &&
        left.DestinationPrefix == right.DestinationPrefix &&
        left.NextHop == right.NextHop &&
        left.InterfaceIndex == right.InterfaceIndex &&
        left.IsPersistent == right.IsPersistent;

    private sealed class MutationCommandResult
    {
        public bool Succeeded { get; init; }
    }
}
