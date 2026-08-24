using System.Net;
using System.Net.Sockets;
using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class RouteMatchService(
    IRouteTableService routeTableService,
    IPowerShellExecutor powerShellExecutor) : IRouteMatchService
{
    private static readonly TimeSpan NativeQueryTimeout = TimeSpan.FromSeconds(15);

    public async Task<RouteMatchResult> MatchAsync(
        string targetAddress,
        CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(targetAddress, out var target) || targetAddress.Contains('/'))
        {
            throw new ArgumentException("请输入有效的 IPv4 或 IPv6 地址。", nameof(targetAddress));
        }

        var normalizedTarget = target.ToString();
        var routes = await routeTableService.GetRoutesAsync(cancellationToken);
        var candidates = routes
            .Where(route => IsSameFamily(route, target.AddressFamily))
            .Select(route => TryCreateCandidate(route, target))
            .Where(candidate => candidate is not null)
            .Cast<RouteCandidate>()
            .OrderByDescending(candidate => candidate.PrefixLength)
            .ThenBy(candidate => candidate.Route.EffectiveMetric)
            .ThenBy(candidate => candidate.Route.RouteMetric)
            .ThenBy(candidate => candidate.Route.InterfaceIndex)
            .ToArray();

        var matchedRoute = candidates.FirstOrDefault()?.Route;
        var nativeRoute = await QueryNativeRouteAsync(normalizedTarget, cancellationToken);
        var isNativeMatch = matchedRoute is not null &&
            string.Equals(matchedRoute.DestinationPrefix, nativeRoute.DestinationPrefix, StringComparison.OrdinalIgnoreCase) &&
            matchedRoute.InterfaceIndex == nativeRoute.InterfaceIndex;

        var decisionReason = matchedRoute is null
            ? "没有找到覆盖目标地址的候选路由。"
            : $"在 {candidates.Length} 条候选路由中选择最长前缀 /{candidates[0].PrefixLength}；" +
              $"同前缀下按路由跃点 {matchedRoute.RouteMetric} + 接口跃点 {matchedRoute.InterfaceMetric}，" +
              $"综合跃点 {matchedRoute.EffectiveMetric} 最小者命中。";

        return new RouteMatchResult(
            normalizedTarget,
            candidates,
            matchedRoute,
            nativeRoute,
            isNativeMatch,
            decisionReason);
    }

    private async Task<NativeRouteMatch> QueryNativeRouteAsync(
        string targetAddress,
        CancellationToken cancellationToken)
    {
        var command = $$"""
            $results = @(Find-NetRoute -RemoteIPAddress '{{targetAddress}}')
            $route = $results | Where-Object { $null -ne $_.DestinationPrefix } | Select-Object -First 1
            [pscustomobject]@{
                DestinationPrefix = [string]$route.DestinationPrefix
                NextHop = [string]$route.NextHop
                InterfaceAlias = [string]$route.InterfaceAlias
                InterfaceIndex = [int]$route.InterfaceIndex
                RouteMetric = [int]$route.RouteMetric
                InterfaceMetric = [int]$route.InterfaceMetric
            }
            """;

        var data = await powerShellExecutor.ExecuteAsync<NativeRouteData>(
            command,
            NativeQueryTimeout,
            cancellationToken);
        return new NativeRouteMatch(
            data.DestinationPrefix,
            data.NextHop,
            data.InterfaceAlias,
            data.InterfaceIndex,
            data.RouteMetric,
            data.InterfaceMetric);
    }

    private static RouteCandidate? TryCreateCandidate(RouteInfo route, IPAddress target)
    {
        var parts = route.DestinationPrefix.Split('/', 2);
        if (parts.Length != 2 ||
            !IPAddress.TryParse(parts[0], out var network) ||
            !int.TryParse(parts[1], out var prefixLength) ||
            network.AddressFamily != target.AddressFamily)
        {
            return null;
        }

        var maxBits = target.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        if (prefixLength < 0 || prefixLength > maxBits || !Contains(network, target, prefixLength))
        {
            return null;
        }

        return new RouteCandidate(
            route,
            prefixLength,
            $"目标地址匹配 {route.DestinationPrefix}，前缀长度 /{prefixLength}，综合跃点 {route.EffectiveMetric}");
    }

    private static bool Contains(IPAddress network, IPAddress target, int prefixLength)
    {
        var networkBytes = network.GetAddressBytes();
        var targetBytes = target.GetAddressBytes();
        var wholeBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;

        for (var index = 0; index < wholeBytes; index++)
        {
            if (networkBytes[index] != targetBytes[index])
            {
                return false;
            }
        }

        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (networkBytes[wholeBytes] & mask) == (targetBytes[wholeBytes] & mask);
    }

    private static bool IsSameFamily(RouteInfo route, AddressFamily family) =>
        (route.AddressFamily == RouteAddressFamily.IPv4 && family == AddressFamily.InterNetwork) ||
        (route.AddressFamily == RouteAddressFamily.IPv6 && family == AddressFamily.InterNetworkV6);

    private sealed class NativeRouteData
    {
        public string DestinationPrefix { get; init; } = string.Empty;
        public string NextHop { get; init; } = string.Empty;
        public string InterfaceAlias { get; init; } = string.Empty;
        public int InterfaceIndex { get; init; }
        public int RouteMetric { get; init; }
        public int InterfaceMetric { get; init; }
    }
}
