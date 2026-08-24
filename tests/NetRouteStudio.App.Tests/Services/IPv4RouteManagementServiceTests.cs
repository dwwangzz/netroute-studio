using System.Text.Json;
using FluentAssertions;
using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class IPv4RouteManagementServiceTests
{
    [Fact]
    public async Task 新增临时OnLink路由_应执行后重新读取并验证()
    {
        var created = Route("10.20.0.0/16", "0.0.0.0", 7, 25, false);
        var table = new StubRouteTableService([created]);
        var executor = new RecordingPowerShellExecutor();
        var service = new IPv4RouteManagementService(executor, table, new StubNetworkAdapterService([Adapter(7)]));

        var result = await service.CreateAsync(new("10.20.0.0/16", "", 7, 25, false));

        result.VerifiedRoute.Should().Be(created);
        table.ReadCount.Should().Be(1);
        executor.Command.Should().Contain("New-NetRoute");
        executor.Command.Should().Contain("-NextHop '0.0.0.0'");
        executor.Command.Should().Contain("-PolicyStore ActiveStore");
    }

    [Fact]
    public async Task 修改路由_脚本应包含失败回滚并重新读取验证()
    {
        var existing = Route("10.20.0.0/16", "192.168.1.1", 7, 10, false);
        var changed = Route("10.30.0.0/16", "192.168.1.254", 7, 20, true);
        var executor = new RecordingPowerShellExecutor();
        var service = new IPv4RouteManagementService(executor, new StubRouteTableService([changed]), new StubNetworkAdapterService([Adapter(7)]));

        var result = await service.UpdateAsync(existing, new("10.30.0.0/16", "192.168.1.254", 7, 20, true));

        result.VerifiedRoute.Should().Be(changed);
        executor.Command.Should().Contain("Remove-NetRoute");
        executor.Command.Should().Contain("-ErrorAction Stop | Out-Null");
        executor.Command.Should().Contain("catch");
        executor.Command.Should().Contain("恢复原路由");
        executor.Command.Should().Contain("netsh.exe interface ipv4 add route");
        executor.Command.Should().Contain("store=persistent");
        executor.Command.Should().NotContain("-PolicyStore PersistentStore");
    }

    [Fact]
    public async Task 新增永久路由_应使用Netsh永久存储并检查退出码()
    {
        var created = Route("10.112.22.0/24", "10.112.45.254", 7, 2, true);
        var executor = new RecordingPowerShellExecutor();
        var service = new IPv4RouteManagementService(
            executor,
            new StubRouteTableService([created]),
            new StubNetworkAdapterService([Adapter(7)]));

        await service.CreateAsync(new("10.112.22.0/24", "10.112.45.254", 7, 2, true));

        executor.Command.Should().Contain("netsh.exe interface ipv4 add route");
        executor.Command.Should().Contain("\"prefix=10.112.22.0/24\"");
        executor.Command.Should().Contain("\"interface=7\"");
        executor.Command.Should().Contain("\"nexthop=10.112.45.254\"");
        executor.Command.Should().Contain("\"metric=2\"");
        executor.Command.Should().Contain("store=persistent");
        executor.Command.Should().Contain("$LASTEXITCODE -ne 0");
        executor.Command.Should().NotContain("New-NetRoute");
        executor.Command.Should().NotContain("PersistentStore");
    }

    [Fact]
    public async Task 新增永久OnLink路由_应省略Netsh下一跳参数()
    {
        var created = Route("198.51.100.0/24", "0.0.0.0", 7, 10, true);
        var executor = new RecordingPowerShellExecutor();
        var service = new IPv4RouteManagementService(
            executor,
            new StubRouteTableService([created]),
            new StubNetworkAdapterService([Adapter(7)]));

        await service.CreateAsync(new("198.51.100.0/24", "", 7, 10, true));

        executor.Command.Should().NotContain("nexthop=");
    }

    [Fact]
    public async Task 删除永久路由_应使用Netsh永久存储()
    {
        var existing = Route("10.112.22.0/24", "10.112.45.254", 7, 2, true);
        var executor = new RecordingPowerShellExecutor();
        var service = new IPv4RouteManagementService(
            executor,
            new StubRouteTableService([]),
            new StubNetworkAdapterService([Adapter(7)]));

        await service.DeleteAsync(existing);

        executor.Command.Should().Contain("netsh.exe interface ipv4 delete route");
        executor.Command.Should().Contain("store=persistent");
        executor.Command.Should().Contain("$LASTEXITCODE -ne 0");
        executor.Command.Should().NotContain("Remove-NetRoute");
    }

    [Fact]
    public async Task 永久路由改为临时_失败回滚应使用Netsh恢复原路由()
    {
        var existing = Route("10.112.22.0/24", "10.112.45.254", 7, 2, true);
        var changed = Route("10.112.22.0/24", "10.112.45.254", 7, 3, false);
        var executor = new RecordingPowerShellExecutor();
        var service = new IPv4RouteManagementService(
            executor,
            new StubRouteTableService([changed]),
            new StubNetworkAdapterService([Adapter(7)]));

        await service.UpdateAsync(existing, new("10.112.22.0/24", "10.112.45.254", 7, 3, false));

        executor.Command.Should().Contain("netsh.exe interface ipv4 delete route");
        executor.Command.Should().Contain("New-NetRoute");
        executor.Command.Should().Contain("恢复原路由");
        executor.Command.Should().Contain("netsh.exe interface ipv4 add route");
    }

    [Fact]
    public async Task 临时路由转永久_永久存储返回接口零_应视为验证成功()
    {
        var existing = Route("10.112.21.0/24", "10.112.45.254", 7, 1, false);
        var persistent = Route("10.112.21.0/24", "10.112.45.254", 0, 1, true) with { IsActive = false };
        var service = new IPv4RouteManagementService(
            new RecordingPowerShellExecutor(),
            new StubRouteTableService([persistent]),
            new StubNetworkAdapterService([Adapter(7)]));

        var result = await service.UpdateAsync(
            existing,
            new("10.112.21.0/24", "10.112.45.254", 7, 1, true));

        result.VerifiedRoute.Should().Be(persistent);
    }

    [Fact]
    public async Task 删除路由_重新读取仍存在_应报告验证失败()
    {
        var existing = Route("10.20.0.0/16", "192.168.1.1", 7, 10, false);
        var service = new IPv4RouteManagementService(
            new RecordingPowerShellExecutor(),
            new StubRouteTableService([existing]),
            new StubNetworkAdapterService([Adapter(7)]));

        var action = () => service.DeleteAsync(existing);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*重新读取*仍然存在*");
    }

    [Fact]
    public async Task 新增路由_接口索引不存在_应在执行PowerShell前拒绝()
    {
        var executor = new RecordingPowerShellExecutor();
        var service = new IPv4RouteManagementService(
            executor,
            new StubRouteTableService([]),
            new StubNetworkAdapterService([Adapter(9)]));

        var action = () => service.CreateAsync(new("198.51.100.0/24", "", 999, 10, false));

        await action.Should().ThrowAsync<ArgumentException>().WithMessage("*接口索引 999 不存在*");
        executor.Command.Should().BeEmpty();
    }

    [Fact]
    public async Task 修改系统路由_不应按管理来源阻止()
    {
        var systemRoute = Route("224.0.0.0/4", "0.0.0.0", 7, 256, false) with { IsUserOperable = false };
        var changed = systemRoute with { RouteMetric = 300 };
        var executor = new RecordingPowerShellExecutor();
        var service = new IPv4RouteManagementService(
            executor,
            new StubRouteTableService([changed]),
            new StubNetworkAdapterService([Adapter(7)]));

        await service.UpdateAsync(systemRoute, new("224.0.0.0/4", "", 7, 300, false));

        executor.Command.Should().Contain("Remove-NetRoute");
    }

    private static RouteInfo Route(
        string prefix,
        string nextHop,
        int interfaceIndex,
        int metric,
        bool persistent) => new(
        RouteAddressFamily.IPv4, prefix, nextHop, "Ethernet", interfaceIndex,
        metric, 25, "NetMgmt", persistent, true);

    private static NetworkAdapterInfo Adapter(int index) => new(
        "Ethernet", "Adapter", index, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, ["192.168.1.10/24"], [], ["1.1.1.1"], ["192.168.1.1"],
        25, false, 25, false);

    private sealed class StubRouteTableService(IReadOnlyList<RouteInfo> routes) : IRouteTableService
    {
        public int ReadCount { get; private set; }

        public Task<IReadOnlyList<RouteInfo>> GetRoutesAsync(CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return Task.FromResult(routes);
        }
    }

    private sealed class RecordingPowerShellExecutor : IPowerShellExecutor
    {
        public string Command { get; private set; } = string.Empty;

        public Task<T> ExecuteAsync<T>(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(JsonSerializer.Deserialize<T>("{\"Succeeded\":true}", new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!);
        }
    }

    private sealed class StubNetworkAdapterService(IReadOnlyList<NetworkAdapterInfo> adapters)
        : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(adapters);
    }
}
