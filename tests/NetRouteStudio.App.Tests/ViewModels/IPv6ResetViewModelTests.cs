using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App.Tests.ViewModels;

public sealed class IPv6ResetViewModelTests
{
    [Fact]
    public async Task 刷新网卡_应过滤无绑定接口并立即生成手工恢复命令()
    {
        var unsupported = Adapter("6to4 Adapter", 3);
        var supported = Adapter("ETH", 7);
        var resetService = new StubResetService(
            [new IPv6BindingInfo("ETH", "ms_tcpip6", true)]);
        var viewModel = new IPv6ResetViewModel(
            new StubAdapterService([unsupported, supported]), new StubIPv4ResetService([]), resetService,
            new AlwaysConfirmService(), NullLogger<IPv6ResetViewModel>.Instance);

        await viewModel.RefreshAsync();

        viewModel.Adapters.Should().ContainSingle().Which.Should().Be(supported);
        viewModel.SelectedAdapter.Should().Be(supported);
        viewModel.BindingStatus.Should().Be("已启用");
        viewModel.ManualRecoveryCommand.Should().Contain("Enable-NetAdapterBinding").And.Contain("'ETH'");
    }

    private static NetworkAdapterInfo Adapter(string name, int index) => new(
        name, "Adapter", index, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Virtual, [], [], [], [], 25, true, 25, true);

    private sealed class StubAdapterService(IReadOnlyList<NetworkAdapterInfo> adapters) : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(adapters);
    }

    private sealed class StubResetService(IReadOnlyList<IPv6BindingInfo> bindings) : IIPv6ResetService
    {
        public string GetResetCommand(string adapterName) => "RESET";

        public string GetManualEnableCommand(string adapterName) =>
            $"Enable-NetAdapterBinding -Name '{adapterName}' -ComponentID ms_tcpip6";

        public Task<IReadOnlyList<IPv6BindingInfo>> GetBindingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(bindings);

        public Task<IPv6BindingInfo> GetBindingAsync(string adapterName, CancellationToken cancellationToken = default) =>
            Task.FromResult(bindings.Single(binding => binding.AdapterName == adapterName));

        public Task<IPv6ResetResult> ResetAsync(
            NetworkAdapterInfo adapter,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new IPv6ResetResult(bindings[0], bindings[0], adapter, false));
    }

    private sealed class StubIPv4ResetService(IReadOnlyList<IPv4BindingInfo> bindings) : IIPv4BindingResetService
    {
        public string GetResetCommand(string adapterName) => "RESET";
        public string GetManualEnableCommand(string adapterName) => $"Enable-NetAdapterBinding -Name '{adapterName}' -ComponentID ms_tcpip";
        public Task<IReadOnlyList<IPv4BindingInfo>> GetBindingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(bindings);
        public Task<IPv4BindingInfo> GetBindingAsync(string adapterName, CancellationToken cancellationToken = default) => Task.FromResult(bindings.Single(binding => binding.AdapterName == adapterName));
        public Task<IPv4BindingResetResult> ResetAsync(NetworkAdapterInfo adapter, IProgress<string>? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IPv4BindingResetResult(bindings[0], bindings[0], adapter, false));
    }

    private sealed class AlwaysConfirmService : IConfirmationService
    {
        public bool Confirm(RouteConfirmationRequest request) => true;
    }
}
