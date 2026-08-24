using System.Net;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class NetworkTestService(INetworkProbe probe, IRouteMatchService routeMatchService) : INetworkTestService
{
    private const int Timeout = 1200;
    private const int PingCount = 4;
    private const int MaxHops = 20;

    public async Task<NetworkTestResult> TestAsync(string input, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        input = input.Trim();
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("请输入要测试的 IP 地址或域名。");
        var isDomain = !IPAddress.TryParse(input, out var directAddress);
        progress?.Report(isDomain ? "正在解析 DNS…" : "已识别 IP 地址…");
        IReadOnlyList<IPAddress> addresses = directAddress is null
            ? (await probe.ResolveAsync(input, cancellationToken)).Distinct().ToArray()
            : [directAddress];
        if (addresses.Count == 0) throw new InvalidOperationException($"域名 {input} 没有解析到 IPv4 或 IPv6 地址。");

        var pings = new List<NetworkPingResult>();
        var matches = new List<RouteMatchResult>();
        foreach (var address in addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"正在测试 {address} 的路由命中和连通性…");
            matches.Add(await routeMatchService.MatchAsync(address.ToString(), cancellationToken));
            for (var sequence = 1; sequence <= PingCount; sequence++)
            {
                var reply = await probe.PingAsync(address, Timeout, 128, cancellationToken);
                pings.Add(new NetworkPingResult(address.ToString(), sequence, reply.Status, reply.RoundtripTime, reply.TimeToLive, reply.ErrorMessage));
            }
        }

        progress?.Report($"正在跟踪到 {addresses[0]} 的网络路径…");
        var hops = new List<TraceRouteHop>();
        for (var ttl = 1; ttl <= MaxHops; ttl++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reply = await probe.PingAsync(addresses[0], Timeout, ttl, cancellationToken);
            hops.Add(new TraceRouteHop(ttl, reply.Address ?? "*", reply.Status, reply.RoundtripTime, reply.ErrorMessage));
            if (reply.Succeeded) break;
        }
        var succeeded = pings.Count(item => item.Status == "Success");
        var summary = $"DNS/IP：{addresses.Count} 个地址；Ping：{succeeded}/{pings.Count} 次成功；路由：{matches.Count} 个地址已完成匹配；Tracert：{hops.Count} 跳。";
        return new NetworkTestResult(input, isDomain, addresses.Select(item => item.ToString()).ToArray(), pings, hops, matches, summary);
    }
}
