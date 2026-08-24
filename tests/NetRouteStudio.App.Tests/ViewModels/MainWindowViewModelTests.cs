using FluentAssertions;
using NetRouteStudio.App.Services;
using NetRouteStudio.App.ViewModels;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void 创建视图模型_管理员身份运行_应显示就绪状态()
    {
        var viewModel = new MainWindowViewModel(
            new StubAdministratorPrivilegeService(true),
            new StubNetworkAdapterService([]));

        viewModel.ApplicationName.Should().Be("NetRoute Studio");
        viewModel.IsRunningAsAdministrator.Should().BeTrue();
        viewModel.PrivilegeStatus.Should().Be("管理员权限：已获取");
        viewModel.StatusMessage.Should().Be("应用基础模块已就绪");
    }

    [Fact]
    public void 创建视图模型_普通用户身份运行_应提示需要管理员权限()
    {
        var viewModel = new MainWindowViewModel(
            new StubAdministratorPrivilegeService(false),
            new StubNetworkAdapterService([]));

        viewModel.IsRunningAsAdministrator.Should().BeFalse();
        viewModel.PrivilegeStatus.Should().Be("管理员权限：未获取");
        viewModel.StatusMessage.Should().Be("网络修改功能需要管理员权限");
    }

    private sealed class StubAdministratorPrivilegeService(bool isAdministrator)
        : IAdministratorPrivilegeService
    {
        public bool IsRunningAsAdministrator() => isAdministrator;
    }

    private sealed class StubNetworkAdapterService(IReadOnlyList<NetworkAdapterInfo> adapters)
        : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(adapters);
    }
}
