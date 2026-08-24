using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App.Tests.ViewModels;

public sealed class IPv4InterfaceMetricViewModelTests
{
    [Fact]
    public async Task 修改手动跃点_应确认命令并同步验证后的网卡()
    {
        var before = Adapter(25, true);
        var after = Adapter(10, false);
        var confirmation = new RecordingConfirmationService();
        var metricService = new StubMetricService(after);
        var viewModel = new IPv4InterfaceMetricViewModel(
            new StubAdapterService([before]), metricService, confirmation,
            NullLogger<IPv4InterfaceMetricViewModel>.Instance);
        await viewModel.RefreshAsync();
        viewModel.AutomaticMetric = false;
        viewModel.ManualMetric = "10";

        await viewModel.UpdateCommand.ExecuteAsync(null);

        confirmation.Request.Should().NotBeNull();
        confirmation.Request!.Command.Should().Be("SET METRIC COMMAND");
        confirmation.Request.Fields.Should().Contain(field =>
            field.Name == "IPv4 接口 Metric" && field.BeforeValue == "25" && field.AfterValue == "10");
        metricService.Request.Should().Be(new IPv4InterfaceMetricRequest(7, false, 10));
        viewModel.SelectedAdapter.Should().Be(after);
        viewModel.Adapters.Should().ContainSingle().Which.Should().Be(after);
    }

    private static NetworkAdapterInfo Adapter(int metric, bool automatic) => new(
        "Ethernet", "Adapter", 7, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, ["192.168.1.10/24"], [], [], ["192.168.1.1"],
        metric, automatic, 25, true);

    private sealed class StubAdapterService(IReadOnlyList<NetworkAdapterInfo> adapters) : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(adapters);
    }

    private sealed class StubMetricService(NetworkAdapterInfo verifiedAdapter) : IIPv4InterfaceMetricService
    {
        public IPv4InterfaceMetricRequest? Request { get; private set; }

        public string GetUpdateCommand(IPv4InterfaceMetricRequest request) => "SET METRIC COMMAND";

        public Task<InterfaceMetricMutationResult> UpdateAsync(
            IPv4InterfaceMetricRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new InterfaceMetricMutationResult("修改成功", verifiedAdapter));
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
}
