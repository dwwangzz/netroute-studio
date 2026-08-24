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
        var service = new IPv4RouteManagementService(executor, table);

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
        var service = new IPv4RouteManagementService(executor, new StubRouteTableService([changed]));

        var result = await service.UpdateAsync(existing, new("10.30.0.0/16", "192.168.1.254", 7, 20, true));

        result.VerifiedRoute.Should().Be(changed);
        executor.Command.Should().Contain("Remove-NetRoute");
        executor.Command.Should().Contain("catch");
        executor.Command.Should().Contain("恢复原路由");
    }

    [Fact]
    public async Task 删除路由_重新读取仍存在_应报告验证失败()
    {
        var existing = Route("10.20.0.0/16", "192.168.1.1", 7, 10, false);
        var service = new IPv4RouteManagementService(
            new RecordingPowerShellExecutor(),
            new StubRouteTableService([existing]));

        var action = () => service.DeleteAsync(existing);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*重新读取*仍然存在*");
    }

    private static RouteInfo Route(
        string prefix,
        string nextHop,
        int interfaceIndex,
        int metric,
        bool persistent) => new(
        RouteAddressFamily.IPv4, prefix, nextHop, "Ethernet", interfaceIndex,
        metric, 25, "NetMgmt", persistent, true);

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
}
