using FluentAssertions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class BatchRouteTextParserTests
{
    [Fact]
    public void 解析多行新增_应支持永久和临时标记()
    {
        const string text = "10.20.0.0/16,192.168.1.1,7,10,永久\n198.51.100.0/24,0.0.0.0,7,20,false";

        var items = BatchRouteTextParser.ParseCreates(text);

        items.Should().HaveCount(2);
        items[0].Operation.Should().Be(BatchRouteOperation.Create);
        items[0].IsPersistent.Should().BeTrue();
        items[1].BuildRequest().NextHop.Should().Be("0.0.0.0");
        items[1].IsPersistent.Should().BeFalse();
    }

    [Fact]
    public void 解析错误行_应报告具体行号()
    {
        var action = () => BatchRouteTextParser.ParseCreates("10.20.0.0/16,192.168.1.1,7");

        action.Should().Throw<FormatException>().WithMessage("*第 1 行*");
    }

    [Fact]
    public void 选择网络接口_应自动同步只读索引和默认网关()
    {
        var adapter = new NetworkAdapterInfo(
            "Ethernet", "Adapter", 7, "Up", "00-00-00-00-00-00", "1 Gbps",
            NetworkAdapterKind.Physical, ["192.168.1.10/24"], [], [], ["192.168.1.1"],
            25, false, 25, false);
        var item = new BatchRouteEditItem();

        item.SelectedAdapter = adapter;

        item.InterfaceIndex.Should().Be("7");
        item.NextHop.Should().Be("192.168.1.1");
    }

    [Fact]
    public void 选择无网关网络接口_应自动使用OnLink下一跳()
    {
        var adapter = new NetworkAdapterInfo(
            "Ethernet", "Adapter", 9, "Up", "00-00-00-00-00-00", "1 Gbps",
            NetworkAdapterKind.Physical, ["198.51.100.10/24"], [], [], [],
            25, false, 25, false);
        var item = new BatchRouteEditItem();

        item.SelectedAdapter = adapter;

        item.InterfaceIndex.Should().Be("9");
        item.NextHop.Should().Be("0.0.0.0");
    }

    [Fact]
    public void 复制路由行_应保留参数并转换为已勾选的新增行()
    {
        var adapter = new NetworkAdapterInfo(
            "Ethernet", "Adapter", 7, "Up", "00-00-00-00-00-00", "1 Gbps",
            NetworkAdapterKind.Physical, ["192.168.1.10/24"], [], [], ["192.168.1.1"],
            25, false, 25, false);
        var originalRoute = new RouteInfo(
            RouteAddressFamily.IPv4, "10.20.0.0/16", "192.168.1.1", "Ethernet", 7,
            10, 25, "NetMgmt", true, true);
        var source = BatchRouteEditItem.FromRoute(originalRoute);
        source.SelectedAdapter = adapter;
        source.Operation = BatchRouteOperation.Delete;

        var copy = source.CopyAsCreate();

        copy.OriginalRoute.Should().BeNull();
        copy.Operation.Should().Be(BatchRouteOperation.Create);
        copy.IsSelected.Should().BeTrue();
        copy.DestinationPrefix.Should().Be("10.20.0.0/16");
        copy.SelectedAdapter.Should().BeSameAs(adapter);
        copy.InterfaceIndex.Should().Be("7");
        copy.IsPersistent.Should().BeTrue();
    }

    [Fact]
    public void 移除已有路由_应切换为可撤销的删除操作()
    {
        var route = new RouteInfo(
            RouteAddressFamily.IPv4, "10.20.0.0/16", "192.168.1.1", "Ethernet", 7,
            10, 25, "NetMgmt", false, true);
        var item = BatchRouteEditItem.FromRoute(route);

        item.ToggleRemoval().Should().BeTrue();
        item.Operation.Should().Be(BatchRouteOperation.Delete);
        item.IsSelected.Should().BeTrue();
        item.IsRouteEditable.Should().BeFalse();
        item.RowActionDisplay.Should().Be("取消删除");

        item.ToggleRemoval().Should().BeTrue();
        item.Operation.Should().Be(BatchRouteOperation.Update);
        item.IsSelected.Should().BeFalse();
        item.IsRouteEditable.Should().BeTrue();
        item.RowActionDisplay.Should().Be("移除行");
    }

    [Fact]
    public void 移除新增行_应通知界面直接删除该行()
    {
        var item = new BatchRouteEditItem { Operation = BatchRouteOperation.Create };

        item.ToggleRemoval().Should().BeFalse();
        item.Operation.Should().Be(BatchRouteOperation.Create);
    }
}
