using System.Text.Json;
using FluentAssertions;
using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class IPv6InterfaceMetricServiceTests
{
    [Fact]
    public async Task 设置IPv6手动跃点_应执行IPv6命令并重新读取验证()
    {
        var before = Adapter(25, true);
        var after = Adapter(12, false);
        var executor = new RecordingExecutor();
        var service = new IPv6InterfaceMetricService(
            executor, new SequenceAdapterService([before], [after]));

        var result = await service.UpdateAsync(new IPv6InterfaceMetricRequest(7, false, 12));

        result.VerifiedAdapter.Should().Be(after);
        executor.Command.Should().Contain("-AddressFamily IPv6");
        executor.Command.Should().Contain("-AutomaticMetric Disabled");
        executor.Command.Should().Contain("-InterfaceMetric 12");
        executor.Command.Should().NotContain("-AddressFamily IPv4");
    }

    [Fact]
    public async Task 启用IPv6自动跃点_命令不应包含手动值()
    {
        var before = Adapter(12, false);
        var after = Adapter(25, true);
        var executor = new RecordingExecutor();
        var service = new IPv6InterfaceMetricService(
            executor, new SequenceAdapterService([before], [after]));

        await service.UpdateAsync(new IPv6InterfaceMetricRequest(7, true, 99));

        executor.Command.Should().Contain("-AutomaticMetric Enabled");
        executor.Command.Should().NotContain("-InterfaceMetric");
    }

    private static NetworkAdapterInfo Adapter(int metric, bool automatic) => new(
        "Ethernet", "Adapter", 7, "Up", "00-00-00-00-00-00", "1 Gbps",
        NetworkAdapterKind.Physical, ["192.168.1.10/24"], ["2001:db8::10/64"], [], ["192.168.1.1"],
        25, true, metric, automatic);

    private sealed class SequenceAdapterService(params IReadOnlyList<NetworkAdapterInfo>[] values)
        : INetworkAdapterService
    {
        private int _readCount;

        public Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default)
        {
            var value = values[Math.Min(_readCount, values.Length - 1)];
            _readCount++;
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
