using FluentAssertions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App.Tests.ViewModels;

public sealed class RouteTableViewModelTests
{
    [Fact]
    public async Task 刷新路由_超过每页数量_应正确分页()
    {
        var routes = Enumerable.Range(1, 30).Select(index => CreateRoute(index)).ToArray();
        var viewModel = new RouteTableViewModel(new StubRouteTableService(routes));

        await viewModel.RefreshRoutesAsync();

        viewModel.VisibleRoutes.Should().HaveCount(25);
        viewModel.PageNumber.Should().Be(1);
        viewModel.TotalPages.Should().Be(2);
        viewModel.NextPageCommand.Execute(null);
        viewModel.VisibleRoutes.Should().HaveCount(5);
        viewModel.PageNumber.Should().Be(2);
    }

    [Fact]
    public async Task 设置协议族和搜索条件_应筛选并重置页码()
    {
        RouteInfo[] routes =
        [
            CreateRoute(1),
            new(RouteAddressFamily.IPv6, "2001:db8::/32", "::", "WLAN", 9, 20, 30, "NetMgmt", false, true)
        ];
        var viewModel = new RouteTableViewModel(new StubRouteTableService(routes));
        await viewModel.RefreshRoutesAsync();

        viewModel.AddressFamilyFilter = "IPv6";
        viewModel.SearchText = "WLAN";

        viewModel.VisibleRoutes.Should().ContainSingle().Which.AddressFamily.Should().Be(RouteAddressFamily.IPv6);
        viewModel.PageNumber.Should().Be(1);
    }

    private static RouteInfo CreateRoute(int index) => new(
        RouteAddressFamily.IPv4, $"10.{index}.0.0/16", "192.168.1.1", "Ethernet", 7,
        index, 25, "NetMgmt", index % 2 == 0, true);

    private sealed class StubRouteTableService(IReadOnlyList<RouteInfo> routes) : IRouteTableService
    {
        public Task<IReadOnlyList<RouteInfo>> GetRoutesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(routes);
    }
}
