using System.Text.Json;
using FluentAssertions;
using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class RouteTableServiceTests
{
    [Fact]
    public async Task 获取路由_应读取活动和永久存储并映射跃点信息()
    {
        const string json = """
            {"Items":[{
              "AddressFamily":"IPv4","DestinationPrefix":"10.0.0.0/8",
              "NextHop":"192.168.1.1","InterfaceAlias":"Ethernet","InterfaceIndex":7,
              "RouteMetric":10,"InterfaceMetric":25,"Protocol":"NetMgmt",
              "IsPersistent":true,"IsUserOperable":true
            }]}
            """;
        var executor = new JsonStubPowerShellExecutor(json);
        var service = new RouteTableService(executor);

        var route = (await service.GetRoutesAsync()).Should().ContainSingle().Subject;

        route.Should().BeEquivalentTo(new RouteInfo(
            RouteAddressFamily.IPv4, "10.0.0.0/8", "192.168.1.1", "Ethernet", 7,
            10, 25, "NetMgmt", true, true));
        executor.Command.Should().Contain("Get-NetRoute -PolicyStore ActiveStore");
        executor.Command.Should().Contain("Get-NetRoute -PolicyStore PersistentStore");
        executor.Command.Should().Contain("Get-NetIPInterface");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task 获取真实Windows路由_应返回合法结构化数据()
    {
        var service = new RouteTableService(
            new PowerShellExecutor(new WindowsPowerShellProcessRunner()));

        var routes = await service.GetRoutesAsync();

        routes.Should().NotBeEmpty();
        routes.Should().OnlyContain(route =>
            !string.IsNullOrWhiteSpace(route.DestinationPrefix) &&
            route.InterfaceIndex > 0 &&
            route.RouteMetric >= 0 &&
            route.InterfaceMetric >= 0);
        routes.Should().Contain(route => route.AddressFamily == RouteAddressFamily.IPv4);
    }

    private sealed class JsonStubPowerShellExecutor(string json) : IPowerShellExecutor
    {
        public string? Command { get; private set; }

        public Task<T> ExecuteAsync<T>(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!);
        }
    }
}
