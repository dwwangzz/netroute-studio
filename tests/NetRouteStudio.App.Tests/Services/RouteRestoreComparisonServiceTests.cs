using FluentAssertions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class RouteRestoreComparisonServiceTests
{
    [Fact]
    public void 比较备份与当前路由_应区分缺失变更一致和仅当前存在()
    {
        var adapter = Adapter(7, "Ethernet");
        var missing = Route("10.1.0.0/16", 7, 10, true);
        var changed = Route("10.2.0.0/16", 7, 10, false);
        var same = Route("10.3.0.0/16", 7, 10, false);
        var currentChanged = changed with { RouteMetric = 20 };
        var currentOnly = Route("10.4.0.0/16", 7, 10, false);
        var backup = Document([missing, changed, same], [adapter]);

        var result = new RouteRestoreComparisonService().Compare(
            backup, [currentChanged, same, currentOnly], [adapter]);

        result.Single(item => item.DestinationPrefix == missing.DestinationPrefix).DifferenceKind
            .Should().Be(RouteRestoreDifferenceKind.Missing);
        result.Single(item => item.DestinationPrefix == changed.DestinationPrefix).DifferenceKind
            .Should().Be(RouteRestoreDifferenceKind.Changed);
        result.Single(item => item.DestinationPrefix == same.DestinationPrefix).DifferenceKind
            .Should().Be(RouteRestoreDifferenceKind.Same);
        var extra = result.Single(item => item.DestinationPrefix == currentOnly.DestinationPrefix);
        extra.DifferenceKind.Should().Be(RouteRestoreDifferenceKind.CurrentOnly);
        extra.IsSelected.Should().BeFalse();
        extra.CanRestore.Should().BeTrue();
    }

    [Fact]
    public void 特殊接口不在网卡列表但路由字段相同_应判定完全一致()
    {
        var loopback = new RouteInfo(
            RouteAddressFamily.IPv4, "127.0.0.0/8", "0.0.0.0",
            "Loopback Pseudo-Interface 1", 1, 256, 75, "Local", false, false);
        var persistent = new RouteInfo(
            RouteAddressFamily.IPv4, "192.168.41.0/24", "192.168.47.60",
            "未绑定（选择接口后生效）", 0, 1, 0, string.Empty, true, false)
            with { IsActive = false };

        var result = new RouteRestoreComparisonService().Compare(
            Document([loopback, persistent], []), [loopback, persistent], []);

        result.Should().OnlyContain(item => item.DifferenceKind == RouteRestoreDifferenceKind.Same);
    }

    [Fact]
    public void 差异列表_所有有变动项目应排在完全一致之前()
    {
        var adapter = Adapter(7, "Ethernet");
        var missing = Route("10.3.0.0/16", 7, 10, false);
        var changed = Route("10.2.0.0/16", 7, 10, false);
        var same = Route("10.1.0.0/16", 7, 10, false);
        var currentOnly = Route("10.4.0.0/16", 7, 10, false);

        var result = new RouteRestoreComparisonService().Compare(
            Document([same, changed, missing], [adapter]),
            [same, changed with { RouteMetric = 20 }, currentOnly],
            [adapter]);

        result.Select(item => item.DifferenceKind).Should().Equal(
            RouteRestoreDifferenceKind.Missing,
            RouteRestoreDifferenceKind.Changed,
            RouteRestoreDifferenceKind.CurrentOnly,
            RouteRestoreDifferenceKind.Same);
    }

    [Fact]
    public void 原接口索引变化但名称相同_应匹配当前网卡()
    {
        var backupAdapter = Adapter(7, "Ethernet");
        var currentAdapter = Adapter(12, "Ethernet");
        var backupRoute = Route("10.1.0.0/16", 7, 10, false);
        var currentRoute = Route("10.1.0.0/16", 12, 10, false);

        var item = new RouteRestoreComparisonService().Compare(
            Document([backupRoute], [backupAdapter]), [currentRoute], [currentAdapter]).Single();

        item.SelectedAdapter.Should().Be(currentAdapter);
        item.DifferenceKind.Should().Be(RouteRestoreDifferenceKind.Same);
    }

    private static RouteInfo Route(string prefix, int index, int metric, bool persistent) => new(
        RouteAddressFamily.IPv4, prefix, "192.168.1.1", "Ethernet", index,
        metric, 25, "NetMgmt", persistent, true);

    private static NetworkAdapterInfo Adapter(int index, string name) => new(
        name, "Adapter", index, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, ["192.168.1.10/24"], [], [], ["192.168.1.1"],
        25, false, 25, true);

    private static NetworkBackupDocument Document(
        IReadOnlyList<RouteInfo> routes,
        IReadOnlyList<NetworkAdapterInfo> adapters) => new(
        "1.0", DateTimeOffset.Now, "UTC", "PC", "Windows", "1.0", routes.Count,
        adapters.Count, routes, adapters, new string('0', 64));
}
