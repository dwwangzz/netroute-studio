using System.Text;
using System.Text.Json;

namespace NetRouteStudio.App.Infrastructure.PowerShell;

public sealed class PowerShellExecutor : IPowerShellExecutor
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPowerShellProcessRunner _processRunner;

    public PowerShellExecutor(IPowerShellProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public async Task<T> ExecuteAsync<T>(
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), "超时时间必须大于零。");
        }

        var encodedCommand = Convert.ToBase64String(Encoding.Unicode.GetBytes(WrapCommand(command)));
        var result = await _processRunner
            .RunAsync(encodedCommand, timeout, cancellationToken)
            .ConfigureAwait(false);

        if (result.TimedOut)
        {
            throw new PowerShellExecutionException(
                PowerShellFailureKind.Timeout,
                $"PowerShell 命令执行超时，限制时间为 {timeout.TotalSeconds:0.###} 秒。");
        }

        if (result.ExitCode != 0)
        {
            var error = string.IsNullOrWhiteSpace(result.StandardError)
                ? "PowerShell 未返回错误详情。"
                : result.StandardError.Trim();
            throw new PowerShellExecutionException(
                PowerShellFailureKind.CommandFailed,
                $"PowerShell 命令执行失败（退出码 {result.ExitCode}）：{error}");
        }

        if (string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            throw new PowerShellExecutionException(
                PowerShellFailureKind.EmptyOutput,
                "PowerShell 命令未返回可反序列化的数据。");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(result.StandardOutput, SerializerOptions)
                ?? throw new JsonException("反序列化结果为空。");
        }
        catch (JsonException exception)
        {
            throw new PowerShellExecutionException(
                PowerShellFailureKind.InvalidJson,
                "PowerShell 返回的数据不是预期的 JSON 结构。",
                exception);
        }
    }

    private static string WrapCommand(string command) => $$"""
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $ErrorActionPreference = 'Stop'
        try {
            $result = & {
        {{command}}
            }
            if ($null -ne $result) {
                $result | ConvertTo-Json -Depth 8 -Compress
            }
        }
        catch {
            [Console]::Error.WriteLine(($_ | Out-String))
            exit 1
        }
        """;
}
