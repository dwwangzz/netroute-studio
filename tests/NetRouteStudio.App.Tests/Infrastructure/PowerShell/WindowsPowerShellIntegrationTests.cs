using FluentAssertions;
using NetRouteStudio.App.Infrastructure.PowerShell;

namespace NetRouteStudio.App.Tests.Infrastructure.PowerShell;

public sealed class WindowsPowerShellIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task 执行真实PowerShell_应返回结构化对象()
    {
        var executor = new PowerShellExecutor(new WindowsPowerShellProcessRunner());

        var result = await executor.ExecuteAsync<PowerShellSample>(
            "[pscustomobject]@{ Name = 'NetRouteStudio'; Value = 8 }",
            TimeSpan.FromSeconds(10));

        result.Should().BeEquivalentTo(new PowerShellSample("NetRouteStudio", 8));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task 执行真实PowerShell_超过限制时间_应终止进程并报告超时()
    {
        var executor = new PowerShellExecutor(new WindowsPowerShellProcessRunner());

        var action = () => executor.ExecuteAsync<object>(
            "Start-Sleep -Seconds 10; [pscustomobject]@{ Completed = $true }",
            TimeSpan.FromMilliseconds(300));

        var exception = await action.Should().ThrowAsync<PowerShellExecutionException>();
        exception.Which.FailureKind.Should().Be(PowerShellFailureKind.Timeout);
    }

    private sealed record PowerShellSample(string Name, int Value);
}
