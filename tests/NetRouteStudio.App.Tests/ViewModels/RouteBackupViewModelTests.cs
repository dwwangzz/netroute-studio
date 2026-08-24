using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App.Tests.ViewModels;

public sealed class RouteBackupViewModelTests
{
    [Fact]
    public async Task 选择性执行_默认不删除额外路由但手动勾选后应删除()
    {
        var adapter = Adapter();
        var missing = Route("10.1.0.0/16");
        var currentOnly = Route("10.2.0.0/16");
        var document = Document([missing], [adapter]);
        var management = new StubManagementService(missing);
        var confirmation = new RecordingConfirmationService();
        var results = new StubResultDialogService();
        var viewModel = new RouteBackupViewModel(
            new StubBackupService(document), new StubFileDialogService(),
            new StubRouteTableService([currentOnly]), new StubAdapterService([adapter]),
            new RouteRestoreComparisonService(), management, confirmation, results,
            NullLogger<RouteBackupViewModel>.Instance);

        await viewModel.OpenBackupCommand.ExecuteAsync(null);
        await viewModel.CompareCurrentCommand.ExecuteAsync(null);
        await viewModel.RestoreSelectedCommand.ExecuteAsync(null);

        management.CreateCount.Should().Be(1);
        management.DeleteCount.Should().Be(0);
        confirmation.Request!.Command.Should().Contain("CREATE ROUTE");
        results.Results.Should().ContainSingle().Which.Succeeded.Should().BeTrue();
        viewModel.RestoreItems.Single(item => item.DestinationPrefix == missing.DestinationPrefix)
            .DifferenceKind.Should().Be(RouteRestoreDifferenceKind.Same);
        viewModel.RestoreItems.Single(item => item.DestinationPrefix == currentOnly.DestinationPrefix)
            .DifferenceKind.Should().Be(RouteRestoreDifferenceKind.CurrentOnly);

        var extra = viewModel.RestoreItems.Single(item => item.DestinationPrefix == currentOnly.DestinationPrefix);
        extra.IsSelected = true;
        await viewModel.RestoreSelectedCommand.ExecuteAsync(null);

        management.DeleteCount.Should().Be(1);
        confirmation.Request!.Command.Should().Contain("DELETE ROUTE");
        extra.DifferenceKind.Should().Be(RouteRestoreDifferenceKind.Deleted);
        extra.IsSelected.Should().BeFalse();
    }

    private static RouteInfo Route(string prefix) => new(
        RouteAddressFamily.IPv4, prefix, "192.168.1.1", "Ethernet", 7,
        10, 25, "NetMgmt", false, true);

    private static NetworkAdapterInfo Adapter() => new(
        "Ethernet", "Adapter", 7, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, ["192.168.1.10/24"], [], [], ["192.168.1.1"],
        25, false, 25, true);

    private static NetworkBackupDocument Document(
        IReadOnlyList<RouteInfo> routes,
        IReadOnlyList<NetworkAdapterInfo> adapters) => new(
        "1.0", DateTimeOffset.Now, "UTC", Environment.MachineName, "Windows", "1.0",
        routes.Count, adapters.Count, routes, adapters, new string('0', 64));

    private sealed class StubBackupService(NetworkBackupDocument document) : IRouteBackupService
    {
        public Task<RouteBackupResult> CreateAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RouteBackupResult(filePath, document));

        public Task<NetworkBackupDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(document);
    }

    private sealed class StubFileDialogService : IRouteBackupFileDialogService
    {
        public string? SelectSavePath(string defaultFileName) => "backup.json";
        public string? SelectOpenPath() => "backup.json";
    }

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

    private sealed class StubManagementService(RouteInfo restored) : IIPv4RouteManagementService
    {
        public int CreateCount { get; private set; }
        public int DeleteCount { get; private set; }

        public string GetCreateCommand(IPv4RouteRequest request) => "CREATE ROUTE";
        public string GetUpdateCommand(RouteInfo existingRoute, IPv4RouteRequest request) => "UPDATE ROUTE";
        public string GetDeleteCommand(RouteInfo route) => "DELETE ROUTE";

        public Task<RouteMutationResult> CreateAsync(IPv4RouteRequest request, CancellationToken cancellationToken = default)
        {
            CreateCount++;
            return Task.FromResult(new RouteMutationResult("恢复成功", restored));
        }

        public Task<RouteMutationResult> UpdateAsync(RouteInfo existingRoute, IPv4RouteRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RouteMutationResult("修改成功", restored));

        public Task<RouteMutationResult> DeleteAsync(RouteInfo route, CancellationToken cancellationToken = default)
        {
            DeleteCount++;
            return Task.FromResult(new RouteMutationResult("删除成功", null));
        }
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

    private sealed class StubResultDialogService : IBatchRouteDialogService
    {
        public IReadOnlyList<BatchRouteExecutionResult> Results { get; private set; } = [];
        public IReadOnlyList<BatchRouteEditItem>? Edit(IReadOnlyList<RouteInfo> routes, IReadOnlyList<NetworkAdapterInfo> adapters) => null;
        public void ShowResults(IReadOnlyList<BatchRouteExecutionResult> results) => Results = results;
    }
}
