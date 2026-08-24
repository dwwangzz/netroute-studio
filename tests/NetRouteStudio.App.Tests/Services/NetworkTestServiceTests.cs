using System.Net;
using FluentAssertions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class NetworkTestServiceTests
{
    [Fact]
    public async Task 域名测试_应汇总全部解析地址的Ping和路由结果()
    {
        var probe = new StubProbe([IPAddress.Parse("192.0.2.1"), IPAddress.Parse("2001:db8::1")]);
        var result = await new NetworkTestService(probe, new StubRouteMatchService()).TestAsync("example.test");
        result.IsDomain.Should().BeTrue();
        result.ResolvedAddresses.Should().HaveCount(2);
        result.PingResults.Should().HaveCount(8);
        result.RouteMatches.Should().HaveCount(2);
        result.TraceHops.Should().HaveCount(2);
    }

    [Fact]
    public async Task IP测试_不应执行Dns解析()
    {
        var probe = new StubProbe([]);
        var result = await new NetworkTestService(probe, new StubRouteMatchService()).TestAsync("192.0.2.8");
        probe.ResolveCalls.Should().Be(0);
        result.ResolvedAddresses.Should().Equal("192.0.2.8");
    }

    [Fact]
    public async Task 空输入_应返回明确提示()
    {
        var action = () => new NetworkTestService(new StubProbe([]), new StubRouteMatchService()).TestAsync("  ");
        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*IP 地址或域名*");
    }

    private sealed class StubProbe(IReadOnlyList<IPAddress> addresses) : INetworkProbe
    {
        private int _traceCalls;
        public int ResolveCalls { get; private set; }
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default) { ResolveCalls++; return Task.FromResult(addresses); }
        public Task<NetworkProbeReply> PingAsync(IPAddress address, int timeoutMilliseconds, int timeToLive, CancellationToken cancellationToken = default)
        {
            if (timeToLive == 128) return Task.FromResult(new NetworkProbeReply("Success", 5, address.ToString(), 64, string.Empty));
            _traceCalls++;
            return Task.FromResult(_traceCalls % 2 == 0
                ? new NetworkProbeReply("Success", 8, address.ToString(), 64, string.Empty)
                : new NetworkProbeReply("TtlExpired", 2, "192.0.2.254", null, "TTL 已到期"));
        }
    }

    private sealed class StubRouteMatchService : IRouteMatchService
    {
        public Task<RouteMatchResult> MatchAsync(string targetAddress, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RouteMatchResult(targetAddress, [], null, new NativeRouteMatch("0.0.0.0/0", "192.0.2.254", "ETH", 7, 1, 25), true, "测试"));
        public async Task<RouteInputMatchResult> MatchInputAsync(string input, CancellationToken cancellationToken = default) =>
            new(input, false, [await MatchAsync(input, cancellationToken)]);
    }
}
