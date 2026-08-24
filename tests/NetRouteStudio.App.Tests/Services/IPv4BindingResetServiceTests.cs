using System.Text.Json;
using FluentAssertions;
using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class IPv4BindingResetServiceTests
{
    [Fact]
    public async Task 重置已启用绑定_应禁用启用并验证最终状态()
    {
        var adapter = Adapter("以太网 测试");
        var executor = new Executor([true, false, true]);
        var result = await new IPv4BindingResetService(executor, new AdapterService([adapter])).ResetAsync(adapter);
        result.After.Enabled.Should().BeTrue();
        executor.Commands.Should().Contain(command => command.Contains("Disable-NetAdapterBinding"));
        executor.Commands.Should().Contain(command => command.Contains("Enable-NetAdapterBinding"));
    }

    [Fact]
    public async Task 首次启用失败_应自动重试()
    {
        var adapter = Adapter("Ethernet");
        var executor = new Executor([true, false, true]) { EnableFailuresRemaining = 1 };
        var result = await new IPv4BindingResetService(executor, new AdapterService([adapter])).ResetAsync(adapter);
        result.EnableRetried.Should().BeTrue();
        executor.Commands.Count(command => command.Contains("Enable-NetAdapterBinding")).Should().Be(2);
    }

    [Fact]
    public async Task 两次启用失败_应包含安全转义的恢复命令()
    {
        var adapter = Adapter("网卡'测试");
        var executor = new Executor([true, false]) { EnableFailuresRemaining = 2 };
        var action = () => new IPv4BindingResetService(executor, new AdapterService([adapter])).ResetAsync(adapter);
        await action.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Enable-NetAdapterBinding*网卡''测试*ms_tcpip*");
    }

    [Fact]
    public async Task 批量读取_应返回MsTcpip绑定()
    {
        var executor = new Executor([]) { AvailableBindings = [new("ETH", "ms_tcpip", true)] };
        var bindings = await new IPv4BindingResetService(executor, new AdapterService([])).GetBindingsAsync();
        bindings.Should().ContainSingle().Which.AdapterName.Should().Be("ETH");
    }

    private static NetworkAdapterInfo Adapter(string name) => new(name, "Adapter", 7, "Up", "00", "1 Gbps", NetworkAdapterKind.Physical, ["192.168.1.10/24"], [], [], ["192.168.1.1"], 25, false, 25, true);

    private sealed class AdapterService(IReadOnlyList<NetworkAdapterInfo> adapters) : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default) => Task.FromResult(adapters);
    }

    private sealed class Executor(IReadOnlyList<bool> states) : IPowerShellExecutor
    {
        private int _readIndex;
        public int EnableFailuresRemaining { get; set; }
        public IReadOnlyList<IPv4BindingInfo> AvailableBindings { get; init; } = [];
        public List<string> Commands { get; } = [];
        public Task<T> ExecuteAsync<T>(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            if (command.Contains("Enable-NetAdapterBinding") && EnableFailuresRemaining-- > 0) throw new InvalidOperationException("模拟启用失败");
            var value = command.Contains("$items = @(")
                ? new { Items = AvailableBindings.Select(binding => new { Name = binding.AdapterName, binding.ComponentId, binding.Enabled }) }
                : command.Contains("Get-NetAdapterBinding")
                    ? (object)new { Name = "Ethernet", ComponentId = "ms_tcpip", Enabled = states[_readIndex++] }
                    : new { Succeeded = true };
            return Task.FromResult(JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!);
        }
    }
}
