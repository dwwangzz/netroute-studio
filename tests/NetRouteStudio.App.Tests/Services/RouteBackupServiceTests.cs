using System.Text;
using System.IO;
using FluentAssertions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class RouteBackupServiceTests
{
    [Fact]
    public async Task 创建并读取备份_应保留IPv4路由网卡信息并通过校验()
    {
        var ipv4 = Route(RouteAddressFamily.IPv4, "10.20.0.0/16");
        var ipv6 = Route(RouteAddressFamily.IPv6, "2001:db8::/32");
        var adapter = Adapter();
        var service = new RouteBackupService(
            new StubRouteTableService([ipv4, ipv6]),
            new StubAdapterService([adapter]));
        var filePath = Path.Combine(Path.GetTempPath(), $"netroute-test-{Guid.NewGuid():N}.json");
        try
        {
            var created = await service.CreateAsync(filePath);
            var loaded = await service.LoadAsync(filePath);

            created.FilePath.Should().Be(Path.GetFullPath(filePath));
            loaded.FormatVersion.Should().Be(RouteBackupService.CurrentFormatVersion);
            loaded.Routes.Should().ContainSingle().Which.Should().Be(ipv4);
            loaded.Adapters.Should().ContainSingle().Which.Should().BeEquivalentTo(adapter);
            loaded.Sha256.Should().MatchRegex("^[0-9a-f]{64}$");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public async Task 备份内容被修改_读取时应拒绝()
    {
        var service = new RouteBackupService(
            new StubRouteTableService([Route(RouteAddressFamily.IPv4, "10.20.0.0/16")]),
            new StubAdapterService([Adapter()]));
        var filePath = Path.Combine(Path.GetTempPath(), $"netroute-test-{Guid.NewGuid():N}.json");
        try
        {
            await service.CreateAsync(filePath);
            var json = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            await File.WriteAllTextAsync(filePath, json.Replace("10.20.0.0/16", "10.30.0.0/16"), Encoding.UTF8);

            var action = () => service.LoadAsync(filePath);

            await action.Should().ThrowAsync<InvalidDataException>().WithMessage("*SHA-256 校验失败*");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    private static RouteInfo Route(RouteAddressFamily family, string prefix) => new(
        family, prefix, family == RouteAddressFamily.IPv4 ? "192.168.1.1" : "fe80::1",
        "Ethernet", 7, 10, 25, "NetMgmt", true, true);

    private static NetworkAdapterInfo Adapter() => new(
        "Ethernet", "Adapter", 7, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, ["192.168.1.10/24"], ["2001:db8::10/64"], ["1.1.1.1"], ["192.168.1.1"],
        25, false, 25, true);

    private sealed class StubRouteTableService(IReadOnlyList<RouteInfo> routes) : IRouteTableService
    {
        public Task<IReadOnlyList<RouteInfo>> GetRoutesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(routes);
    }

    private sealed class StubAdapterService(IReadOnlyList<NetworkAdapterInfo> adapters) : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(adapters);
    }
}
