using FluentAssertions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;

namespace NetRouteStudio.App.Tests.ViewModels;

public sealed class NetworkAdapterViewModelTests
{
    [Fact]
    public async Task 刷新网卡成功_应替换列表并更新状态()
    {
        var adapter = CreateAdapter("Ethernet", 7);
        var viewModel = new MainWindowViewModel(
            new StubAdministratorPrivilegeService(),
            new StubNetworkAdapterService([adapter]));

        await viewModel.RefreshAdaptersAsync();

        viewModel.Adapters.Should().ContainSingle().Which.Should().BeSameAs(adapter);
        viewModel.SelectedAdapter.Should().BeSameAs(adapter);
        viewModel.StatusMessage.Should().Be("已读取 1 个网络适配器");
        viewModel.ErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public async Task 刷新网卡失败_应清空列表并显示错误状态()
    {
        var viewModel = new MainWindowViewModel(
            new StubAdministratorPrivilegeService(),
            new ThrowingNetworkAdapterService());

        await viewModel.RefreshAdaptersAsync();

        viewModel.Adapters.Should().BeEmpty();
        viewModel.StatusMessage.Should().Be("网卡信息读取失败");
        viewModel.ErrorMessage.Should().Contain("模拟读取失败");
    }

    private static NetworkAdapterInfo CreateAdapter(string name, int index) => new(
        name, "Adapter", index, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, [], [], [], [], null, null, null, null);

    private sealed class StubAdministratorPrivilegeService : IAdministratorPrivilegeService
    {
        public bool IsRunningAsAdministrator() => true;
    }

    private sealed class StubNetworkAdapterService(IReadOnlyList<NetworkAdapterInfo> adapters)
        : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(adapters);
    }

    private sealed class ThrowingNetworkAdapterService : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("模拟读取失败");
    }
}
