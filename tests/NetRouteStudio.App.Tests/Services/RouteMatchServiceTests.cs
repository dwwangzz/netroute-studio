using System.Text.Json;
using FluentAssertions;
using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class RouteMatchServiceTests
{
    [Fact]
    public async Task 匹配IPv4_应先最长前缀再比较综合跃点()
    {
        RouteInfo[] routes =
        [
            Route("0.0.0.0/0", 5, 10),
            Route("10.0.0.0/8", 20, 20),
            Route("10.1.0.0/16", 30, 30),
            Route("10.1.0.0/16", 5, 10, "Ethernet 2", 8)
        ];
        var service = CreateService(routes, "10.1.0.0/16", "Ethernet 2", 8);

        var result = await service.MatchAsync("10.1.2.3");

        result.Candidates.Should().HaveCount(4);
        result.MatchedRoute.Should().Be(routes[3]);
        result.IsNativeMatch.Should().BeTrue();
        result.DecisionReason.Should().Contain("最长前缀 /16").And.Contain("综合跃点 15");
    }

    [Fact]
    public async Task 匹配IPv6_不应包含IPv4候选路由()
    {
        RouteInfo[] routes =
        [
            Route("0.0.0.0/0", 1, 1),
            new(RouteAddressFamily.IPv6, "::/0", "fe80::1", "WLAN", 9, 10, 20, "NetMgmt", false, true),
            new(RouteAddressFamily.IPv6, "2001:db8::/32", "::", "WLAN", 9, 50, 20, "NetMgmt", false, true)
        ];
        var service = CreateService(routes, "2001:db8::/32", "WLAN", 9);

        var result = await service.MatchAsync("2001:db8::1234");

        result.Candidates.Should().HaveCount(2).And.OnlyContain(candidate =>
            candidate.Route.AddressFamily == RouteAddressFamily.IPv6);
        result.MatchedRoute!.DestinationPrefix.Should().Be("2001:db8::/32");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-ip")]
    [InlineData("192.168.1.1/24")]
    public async Task 输入无效目标IP_应拒绝匹配(string target)
    {
        var service = CreateService([], string.Empty, string.Empty, 0);

        var action = () => service.MatchAsync(target);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task 匹配真实公网地址_程序结果应与Windows原生结果一致()
    {
        var executor = new PowerShellExecutor(new WindowsPowerShellProcessRunner());
        var service = new RouteMatchService(new RouteTableService(executor), executor);

        var result = await service.MatchAsync("8.8.8.8");

        result.Candidates.Should().NotBeEmpty();
        result.MatchedRoute.Should().NotBeNull();
        result.NativeRoute.DestinationPrefix.Should().NotBeNullOrWhiteSpace();
        result.IsNativeMatch.Should().BeTrue();
    }

    private static RouteMatchService CreateService(
        IReadOnlyList<RouteInfo> routes,
        string nativePrefix,
        string nativeAlias,
        int nativeIndex)
    {
        var json = JsonSerializer.Serialize(new
        {
            DestinationPrefix = nativePrefix,
            NextHop = "0.0.0.0",
            InterfaceAlias = nativeAlias,
            InterfaceIndex = nativeIndex,
            RouteMetric = 5,
            InterfaceMetric = 10
        });
        return new RouteMatchService(new StubRouteTableService(routes), new JsonStubPowerShellExecutor(json));
    }

    private static RouteInfo Route(
        string prefix,
        int routeMetric,
        int interfaceMetric,
        string alias = "Ethernet",
        int index = 7) =>
        new(RouteAddressFamily.IPv4, prefix, "192.168.1.1", alias, index,
            routeMetric, interfaceMetric, "NetMgmt", false, true);

    private sealed class StubRouteTableService(IReadOnlyList<RouteInfo> routes) : IRouteTableService
    {
        public Task<IReadOnlyList<RouteInfo>> GetRoutesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(routes);
    }

    private sealed class JsonStubPowerShellExecutor(string json) : IPowerShellExecutor
    {
        public Task<T> ExecuteAsync<T>(string command, TimeSpan timeout, CancellationToken cancellationToken = default) =>
            Task.FromResult(JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!);
    }
}
