using System.Text.Json;
using FluentAssertions;
using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class IPv4InterfaceMetricServiceTests
{
    [Fact]
    public async Task 设置手动跃点_应执行命令并重新读取验证()
    {
        var before = Adapter(7, 25, true);
        var after = Adapter(7, 10, false);
        var adapters = new SequenceAdapterService([before], [after]);
        var executor = new RecordingExecutor();
        var service = new IPv4InterfaceMetricService(executor, adapters);

        var result = await service.UpdateAsync(new IPv4InterfaceMetricRequest(7, false, 10));

        result.VerifiedAdapter.Should().Be(after);
        adapters.ReadCount.Should().Be(2);
        executor.Command.Should().Contain("Set-NetIPInterface");
        executor.Command.Should().Contain("-AddressFamily IPv4");
        executor.Command.Should().Contain("-AutomaticMetric Disabled");
        executor.Command.Should().Contain("-InterfaceMetric 10");
    }

    [Fact]
    public async Task 启用自动跃点_命令不应继续设置手动值()
    {
        var before = Adapter(7, 10, false);
        var after = Adapter(7, 25, true);
        var executor = new RecordingExecutor();
        var service = new IPv4InterfaceMetricService(
            executor, new SequenceAdapterService([before], [after]));

        await service.UpdateAsync(new IPv4InterfaceMetricRequest(7, true, 99));

        executor.Command.Should().Contain("-AutomaticMetric Enabled");
        executor.Command.Should().NotContain("-InterfaceMetric");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    public void 手动跃点超出范围_应拒绝生成命令(int metric)
    {
        var service = new IPv4InterfaceMetricService(new RecordingExecutor(), new SequenceAdapterService([]));

        var action = () => service.GetUpdateCommand(new IPv4InterfaceMetricRequest(7, false, metric));

        action.Should().Throw<ArgumentException>().WithMessage("*1 到 9999*");
    }

    private static NetworkAdapterInfo Adapter(int index, int metric, bool automatic) => new(
        "Ethernet", "Adapter", index, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, ["192.168.1.10/24"], [], [], ["192.168.1.1"],
        metric, automatic, 25, true);

    private sealed class SequenceAdapterService(params IReadOnlyList<NetworkAdapterInfo>[] values)
        : INetworkAdapterService
    {
        public int ReadCount { get; private set; }

        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default)
        {
            var value = values[Math.Min(ReadCount, values.Length - 1)];
            ReadCount++;
            return Task.FromResult(value);
        }
    }

    private sealed class RecordingExecutor : IPowerShellExecutor
    {
        public string Command { get; private set; } = string.Empty;

        public Task<T> ExecuteAsync<T>(string command, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(JsonSerializer.Deserialize<T>("{\"Succeeded\":true}", new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!);
        }
    }
}
