using System.Text;
using FluentAssertions;
using NetRouteStudio.App.Infrastructure.PowerShell;

namespace NetRouteStudio.App.Tests.Infrastructure.PowerShell;

public sealed class PowerShellExecutorTests
{
    [Fact]
    public async Task 执行命令_返回有效Json_应反序列化结构化结果()
    {
        var runner = new StubPowerShellProcessRunner(
            new PowerShellProcessResult(0, "{\"Name\":\"Ethernet\",\"Index\":7}", string.Empty, false));
        var executor = new PowerShellExecutor(runner);

        var result = await executor.ExecuteAsync<AdapterSample>(
            "[pscustomobject]@{ Name = 'Ethernet'; Index = 7 }",
            TimeSpan.FromSeconds(5));

        result.Should().BeEquivalentTo(new AdapterSample("Ethernet", 7));
        var wrappedCommand = Encoding.Unicode.GetString(Convert.FromBase64String(runner.EncodedCommand!));
        wrappedCommand.Should().Contain("$ErrorActionPreference = 'Stop'");
        wrappedCommand.Should().Contain("ConvertTo-Json -Depth 8 -Compress");
    }

    [Fact]
    public async Task 执行命令_PowerShell返回错误_应抛出包含错误信息的异常()
    {
        var runner = new StubPowerShellProcessRunner(
            new PowerShellProcessResult(1, string.Empty, "Get-NetAdapter: access denied", false));
        var executor = new PowerShellExecutor(runner);

        var action = () => executor.ExecuteAsync<AdapterSample>("Get-NetAdapter", TimeSpan.FromSeconds(5));

        var exception = await action.Should().ThrowAsync<PowerShellExecutionException>();
        exception.Which.FailureKind.Should().Be(PowerShellFailureKind.CommandFailed);
        exception.Which.Message.Should().Contain("access denied");
    }

    [Fact]
    public async Task 执行命令_返回非法Json_应抛出反序列化异常()
    {
        var runner = new StubPowerShellProcessRunner(
            new PowerShellProcessResult(0, "not-json", string.Empty, false));
        var executor = new PowerShellExecutor(runner);

        var action = () => executor.ExecuteAsync<AdapterSample>("Get-NetAdapter", TimeSpan.FromSeconds(5));

        var exception = await action.Should().ThrowAsync<PowerShellExecutionException>();
        exception.Which.FailureKind.Should().Be(PowerShellFailureKind.InvalidJson);
    }

    [Fact]
    public async Task 执行命令_进程超时_应抛出超时异常()
    {
        var runner = new StubPowerShellProcessRunner(
            new PowerShellProcessResult(-1, string.Empty, string.Empty, true));
        var executor = new PowerShellExecutor(runner);

        var action = () => executor.ExecuteAsync<AdapterSample>("Start-Sleep 10", TimeSpan.FromMilliseconds(100));

        var exception = await action.Should().ThrowAsync<PowerShellExecutionException>();
        exception.Which.FailureKind.Should().Be(PowerShellFailureKind.Timeout);
    }

    private sealed record AdapterSample(string Name, int Index);

    private sealed class StubPowerShellProcessRunner(PowerShellProcessResult result) : IPowerShellProcessRunner
    {
        public string? EncodedCommand { get; private set; }

        public Task<PowerShellProcessResult> RunAsync(
            string encodedCommand,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            EncodedCommand = encodedCommand;
            return Task.FromResult(result);
        }
    }
}
