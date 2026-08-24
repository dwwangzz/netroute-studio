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
            new StubAdapterService([before]), metricService, new StubIPv6MetricService(after), confirmation,
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

    [Fact]
    public async Task 修改IPv6手动跃点_应确认IPv6命令并同步网卡()
    {
        var before = Adapter(25, true, 30, true);
        var after = Adapter(25, true, 12, false);
        var confirmation = new RecordingConfirmationService();
        var ipv6Service = new StubIPv6MetricService(after);
        var viewModel = new IPv4InterfaceMetricViewModel(
            new StubAdapterService([before]), new StubMetricService(after), ipv6Service, confirmation,
            NullLogger<IPv4InterfaceMetricViewModel>.Instance);
        await viewModel.RefreshAsync();
        viewModel.Ipv6AutomaticMetric = false;
        viewModel.Ipv6ManualMetric = "12";

        await viewModel.UpdateIPv6Command.ExecuteAsync(null);

        confirmation.Request!.Command.Should().Be("SET IPV6 METRIC COMMAND");
        confirmation.Request.Fields.Should().Contain(field => field.Name == "地址族" && field.AfterValue == "IPv6");
        confirmation.Request.Fields.Should().Contain(field =>
            field.Name == "IPv6 接口 Metric" && field.BeforeValue == "30" && field.AfterValue == "12");
        ipv6Service.Request.Should().Be(new IPv6InterfaceMetricRequest(7, false, 12));
        viewModel.SelectedAdapter.Should().Be(after);
    }

    private static NetworkAdapterInfo Adapter(
        int metric,
        bool automatic,
        int ipv6Metric = 25,
        bool ipv6Automatic = true) => new(
        "Ethernet", "Adapter", 7, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, ["192.168.1.10/24"], ["2001:db8::10/64"], [], ["192.168.1.1"],
        metric, automatic, ipv6Metric, ipv6Automatic);

    private sealed class StubAdapterService(IReadOnlyList<NetworkAdapterInfo> adapters) : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(adapters);
    }

    private sealed class StubIPv6MetricService(NetworkAdapterInfo verifiedAdapter) : IIPv6InterfaceMetricService
    {
        public IPv6InterfaceMetricRequest? Request { get; private set; }

        public string GetUpdateCommand(IPv6InterfaceMetricRequest request) => "SET IPV6 METRIC COMMAND";

        public Task<InterfaceMetricMutationResult> UpdateAsync(
            IPv6InterfaceMetricRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new InterfaceMetricMutationResult("IPv6 修改成功", verifiedAdapter));
        }
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
