using System.Text.Json;
using FluentAssertions;
using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class IPv6ResetServiceTests
{
    [Fact]
    public async Task 重置已启用绑定_应禁用启用并重新读取验证()
    {
        var adapter = Adapter("以太网 测试");
        var executor = new ResetExecutor([true, false, true]);
        var service = new IPv6ResetService(executor, new StubAdapterService([adapter]));

        var result = await service.ResetAsync(adapter);

        result.Before.Enabled.Should().BeTrue();
        result.After.Enabled.Should().BeTrue();
        result.EnableRetried.Should().BeFalse();
        executor.Commands.Should().Contain(command => command.Contains("Disable-NetAdapterBinding"));
        executor.Commands.Should().Contain(command => command.Contains("Enable-NetAdapterBinding"));
    }

    [Fact]
    public async Task 首次启用失败_应自动重试并报告已重试()
    {
        var adapter = Adapter("Ethernet");
        var executor = new ResetExecutor([true, false, true]) { EnableFailuresRemaining = 1 };
        var service = new IPv6ResetService(executor, new StubAdapterService([adapter]));

        var result = await service.ResetAsync(adapter);

        result.EnableRetried.Should().BeTrue();
        executor.Commands.Count(command => command.Contains("Enable-NetAdapterBinding")).Should().Be(2);
    }

    [Fact]
    public async Task 两次启用均失败_应提供可复制的手工恢复命令()
    {
        var adapter = Adapter("网卡'测试");
        var executor = new ResetExecutor([true, false]) { EnableFailuresRemaining = 2 };
        var service = new IPv6ResetService(executor, new StubAdapterService([adapter]));

        var action = () => service.ResetAsync(adapter);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*可能仍处于禁用状态*Enable-NetAdapterBinding*网卡''测试*");
    }

    [Fact]
    public void 命令预览_应安全转义含单引号的网卡名称()
    {
        var service = new IPv6ResetService(new ResetExecutor([]), new StubAdapterService([]));

        var command = service.GetResetCommand("网卡'测试");

        command.Should().Contain("-Name '网卡''测试'");
        command.Should().Contain("Disable-NetAdapterBinding");
        command.Should().Contain("Enable-NetAdapterBinding");
    }

    [Fact]
    public async Task 批量读取绑定_应只返回实际存在的MsTcpip6绑定实例()
    {
        var executor = new ResetExecutor([])
        {
            AvailableBindings =
            [
                new IPv6BindingInfo("ETH", "ms_tcpip6", true),
                new IPv6BindingInfo("WLAN", "ms_tcpip6", false)
            ]
        };
        var service = new IPv6ResetService(executor, new StubAdapterService([]));

        var bindings = await service.GetBindingsAsync();

        bindings.Select(binding => binding.AdapterName).Should().Equal("ETH", "WLAN");
    }

    private static NetworkAdapterInfo Adapter(string name) => new(
        name, "Adapter", 7, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, ["192.168.1.10/24"], ["2001:db8::10/64"], [], ["192.168.1.1"],
        25, false, 25, true);

    private sealed class StubAdapterService(IReadOnlyList<NetworkAdapterInfo> adapters) : INetworkAdapterService
    {
        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(adapters);
    }

    private sealed class ResetExecutor(IReadOnlyList<bool> bindingStates) : IPowerShellExecutor
    {
        private int _bindingReadIndex;
        public int EnableFailuresRemaining { get; set; }
        public IReadOnlyList<IPv6BindingInfo> AvailableBindings { get; init; } = [];
        public List<string> Commands { get; } = [];

        public Task<T> ExecuteAsync<T>(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Commands.Add(command);
            if (command.Contains("Enable-NetAdapterBinding") && EnableFailuresRemaining > 0)
            {
                EnableFailuresRemaining--;
                throw new InvalidOperationException("模拟启用失败");
            }

            var json = command.Contains("$items = @(")
                ? JsonSerializer.Serialize(new
                {
                    Items = AvailableBindings.Select(binding => new
                    {
                        Name = binding.AdapterName,
                        binding.ComponentId,
                        binding.Enabled
                    })
                })
                : command.Contains("Get-NetAdapterBinding")
                ? JsonSerializer.Serialize(new
                {
                    Name = "Ethernet",
                    ComponentId = "ms_tcpip6",
                    Enabled = bindingStates[_bindingReadIndex++]
                })
                : "{\"Succeeded\":true}";
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!);
        }
    }
}
