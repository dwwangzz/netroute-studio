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

    [Fact]
    public async Task 点击新增_确认框应展示全部字段和实际命令()
    {
        var adapter = Adapter(7, "ETH", ["192.168.1.1"]);
        var confirmation = new RecordingConfirmationService();
        var management = new StubManagementService(Route("198.51.100.0/24", "ETH", 7));
        var viewModel = CreateViewModel([], [adapter], management: management, confirmation: confirmation);
        await viewModel.RefreshAsync();
        viewModel.DestinationPrefix = "198.51.100.0/24";
        viewModel.SelectedAdapter = adapter;
        viewModel.RouteMetric = "10";
        viewModel.IsPersistent = true;

        await viewModel.CreateCommand.ExecuteAsync(null);

        confirmation.Request.Should().NotBeNull();
        confirmation.Request!.Fields.Should().Contain(field => field.Name == "有效 Metric" && field.AfterValue == "35");
        confirmation.Request.Fields.Should().Contain(field => field.Name == "保存方式" && field.AfterValue == "永久");
        confirmation.Request.Command.Should().Be("CREATE COMMAND");
    }

    [Fact]
    public async Task 批量新增_应确认命令逐条执行并同步前端()
    {
        var created = Route("203.0.113.0/24", "ETH", 7);
        var item = new BatchRouteEditItem
        {
            IsSelected = true,
            Operation = BatchRouteOperation.Create,
            DestinationPrefix = "203.0.113.0/24",
            NextHop = "0.0.0.0",
            InterfaceIndex = "7",
            RouteMetric = "10"
        };
        var dialog = new StubBatchRouteDialogService([item]);
        var confirmation = new RecordingConfirmationService();
        var management = new StubManagementService(created);
        var viewModel = CreateViewModel([], [Adapter(7, "ETH", [])], management: management,
            confirmation: confirmation, batchDialog: dialog);
        await viewModel.RefreshAsync();

        await viewModel.BatchManageCommand.ExecuteAsync(null);

        confirmation.Request!.Command.Should().Contain("CREATE COMMAND");
        management.LastCreatedRequest!.DestinationPrefix.Should().Be("203.0.113.0/24");
        viewModel.Routes.Should().ContainSingle().Which.Should().Be(created);
        dialog.Results.Should().ContainSingle().Which.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task 批量删除_单条失败不应阻断后续操作()
    {
        var failedRoute = Route("10.1.0.0/16", "ETH", 7);
        var succeededRoute = Route("10.2.0.0/16", "ETH", 7);
        var items = new[]
        {
            BatchRouteEditItem.FromRoute(failedRoute),
            BatchRouteEditItem.FromRoute(succeededRoute)
        };
        foreach (var item in items)
        {
            item.IsSelected = true;
            item.Operation = BatchRouteOperation.Delete;
        }
        var dialog = new StubBatchRouteDialogService(items);
        var management = new StubManagementService(null) { FailureDestinationPrefix = failedRoute.DestinationPrefix };
        var viewModel = CreateViewModel([failedRoute, succeededRoute], [Adapter(7, "ETH", [])],
            management: management, batchDialog: dialog);
        await viewModel.RefreshAsync();

        await viewModel.BatchManageCommand.ExecuteAsync(null);

        dialog.Results.Should().HaveCount(2);
        dialog.Results.Should().Contain(result => !result.Succeeded && result.DestinationPrefix == failedRoute.DestinationPrefix);
        dialog.Results.Should().Contain(result => result.Succeeded && result.DestinationPrefix == succeededRoute.DestinationPrefix);
        viewModel.Routes.Should().Contain(failedRoute).And.NotContain(succeededRoute);
    }

    private static RouteManagementViewModel CreateViewModel(
        IReadOnlyList<RouteInfo> routes,
        IReadOnlyList<NetworkAdapterInfo> adapters,
        StubRouteTableService? table = null,
        StubManagementService? management = null,
        IConfirmationService? confirmation = null,
        IBatchRouteDialogService? batchDialog = null) => new(
        table ?? new StubRouteTableService(routes),
        new StubNetworkAdapterService(adapters),
        management ?? new StubManagementService(null),
        confirmation ?? new AlwaysConfirmService(),
        batchDialog ?? new StubBatchRouteDialogService(null),
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
        public string? FailureDestinationPrefix { get; init; }

        public string GetCreateCommand(IPv4RouteRequest request) => "CREATE COMMAND";

        public string GetUpdateCommand(RouteInfo existingRoute, IPv4RouteRequest request) => "UPDATE COMMAND";

        public string GetDeleteCommand(RouteInfo route) => "DELETE COMMAND";

        public Task<RouteMutationResult> CreateAsync(IPv4RouteRequest request, CancellationToken cancellationToken = default)
        {
            LastCreatedRequest = request;
            return Task.FromResult(new RouteMutationResult("新增成功", createdRoute));
        }

        public Task<RouteMutationResult> UpdateAsync(RouteInfo existingRoute, IPv4RouteRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RouteMutationResult("修改成功", createdRoute));

        public Task<RouteMutationResult> DeleteAsync(RouteInfo route, CancellationToken cancellationToken = default)
        {
            if (route.DestinationPrefix == FailureDestinationPrefix)
            {
                throw new InvalidOperationException("模拟删除失败");
            }

            return Task.FromResult(new RouteMutationResult("删除成功", null));
        }
    }

    private sealed class AlwaysConfirmService : IConfirmationService
    {
        public bool Confirm(RouteConfirmationRequest request) => true;
    }

    private sealed class RecordingConfirmationService : IConfirmationService
    {
        public RouteConfirmationRequest? Request { get; private set; }

        public bool Confirm(RouteConfirmationRequest request)
        {
            Request = request;
            return true;
        }
    }

    private sealed class StubBatchRouteDialogService(IReadOnlyList<BatchRouteEditItem>? items)
        : IBatchRouteDialogService
    {
        public IReadOnlyList<BatchRouteExecutionResult> Results { get; private set; } = [];

        public IReadOnlyList<BatchRouteEditItem>? Edit(
            IReadOnlyList<RouteInfo> routes,
            IReadOnlyList<NetworkAdapterInfo> adapters) => items;

        public void ShowResults(IReadOnlyList<BatchRouteExecutionResult> results) => Results = results;
    }
}
