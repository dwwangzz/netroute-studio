using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App.Tests.ViewModels;

public sealed class RouteManagementViewModelTests
{
    [Fact]
    public async Task 搜索路由_应按目标和接口实时过滤()
    {
        RouteInfo[] routes =
        [
            Route("10.1.0.0/16", "ETH", 7),
            Route("192.168.8.0/24", "WLAN", 9)
        ];
        var viewModel = CreateViewModel(routes, [Adapter(7, "ETH", ["10.1.0.1"])]);
        await viewModel.RefreshAsync();

        viewModel.SearchText = "WLAN";

        viewModel.Routes.Should().ContainSingle().Which.DestinationPrefix.Should().Be("192.168.8.0/24");
    }

    [Fact]
    public async Task 新增成功_应同步前端且不再次读取整张路由表()
    {
        var table = new StubRouteTableService([]);
        var adapter = Adapter(7, "ETH", []);
        var created = Route("198.51.100.0/24", "ETH", 7);
        var management = new StubManagementService(created);
        var viewModel = CreateViewModel([], [adapter], table, management);
        await viewModel.RefreshAsync();
        viewModel.DestinationPrefix = "198.51.100.0/24";
        viewModel.SelectedAdapter = adapter;
        viewModel.RouteMetric = "10";

        await viewModel.CreateCommand.ExecuteAsync(null);

        table.ReadCount.Should().Be(1);
        viewModel.Routes.Should().ContainSingle().Which.Should().Be(created);
        management.LastCreatedRequest.Should().NotBeNull();
        management.LastCreatedRequest!.NextHop.Should().Be("0.0.0.0");
    }

    private static RouteManagementViewModel CreateViewModel(
        IReadOnlyList<RouteInfo> routes,
        IReadOnlyList<NetworkAdapterInfo> adapters,
        StubRouteTableService? table = null,
        StubManagementService? management = null) => new(
        table ?? new StubRouteTableService(routes),
        new StubNetworkAdapterService(adapters),
        management ?? new StubManagementService(null),
        new AlwaysConfirmService(),
        NullLogger<RouteManagementViewModel>.Instance);

    private static RouteInfo Route(string prefix, string alias, int index) => new(
        RouteAddressFamily.IPv4, prefix, "0.0.0.0", alias, index,
        10, 25, "NetMgmt", false, true);

    private static NetworkAdapterInfo Adapter(
        int index,
        string name,
        IReadOnlyList<string> gateways) => new(
        name, "Adapter", index, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, ["192.168.1.10/24"], [], [], gateways,
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

    private sealed class StubNetworkAdapterService(IReadOnlyList<NetworkAdapterInfo> adapters)
        : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(adapters);
    }

    private sealed class StubManagementService(RouteInfo? createdRoute) : IIPv4RouteManagementService
    {
        public IPv4RouteRequest? LastCreatedRequest { get; private set; }

        public Task<RouteMutationResult> CreateAsync(IPv4RouteRequest request, CancellationToken cancellationToken = default)
        {
            LastCreatedRequest = request;
            return Task.FromResult(new RouteMutationResult("新增成功", createdRoute));
        }

        public Task<RouteMutationResult> UpdateAsync(RouteInfo existingRoute, IPv4RouteRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RouteMutationResult("修改成功", createdRoute));

        public Task<RouteMutationResult> DeleteAsync(RouteInfo route, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RouteMutationResult("删除成功", null));
    }

    private sealed class AlwaysConfirmService : IConfirmationService
    {
        public bool Confirm(string title, string message) => true;
    }
}
