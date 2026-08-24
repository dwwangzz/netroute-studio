using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed partial class ControlledCommandService : IControlledCommandService
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { "ping", "tracert", "ipconfig", "route", "arp", "nslookup", "netstat" };
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public ControlledCommand Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("请输入要执行的白名单网络命令。");
        if (UnsafeCharacterRegex().IsMatch(input)) throw new ArgumentException("命令包含连接符、重定向符或换行，已拒绝执行。");
        var tokens = Tokenize(input);
        if (tokens.Count == 0 || !Allowed.Contains(tokens[0])) throw new ArgumentException("仅允许 ping、tracert、ipconfig、route print、arp -a、nslookup 和 netstat。 ");
        var command = tokens[0].ToLowerInvariant();
        var arguments = tokens.Skip(1).ToArray();
        Validate(command, arguments);
        return new ControlledCommand(string.Join(" ", tokens), command + ".exe", arguments);
    }

    public async Task<ControlledCommandResult> ExecuteAsync(string input, IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default)
    {
        var command = Parse(input);
        var started = DateTimeOffset.Now;
        using var process = new Process { StartInfo = CreateStartInfo(command) };
        if (!process.Start()) throw new InvalidOperationException($"无法启动 {command.Executable}。");
        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();
        var outputTask = ReadLinesAsync(process.StandardOutput, standardOutput, outputProgress, string.Empty);
        var errorTask = ReadLinesAsync(process.StandardError, standardError, outputProgress, "[错误] ");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        var timedOut = false;
        var cancelled = false;
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException)
        {
            timedOut = !cancellationToken.IsCancellationRequested;
            cancelled = !timedOut;
            if (!process.HasExited) process.Kill(true);
            await process.WaitForExitAsync(CancellationToken.None);
        }
        await Task.WhenAll(outputTask, errorTask);
        return new ControlledCommandResult(command.DisplayCommand, started, DateTimeOffset.Now - started,
            timedOut || cancelled ? -1 : process.ExitCode, standardOutput.ToString(), standardError.ToString(), timedOut, cancelled);
    }

    private static ProcessStartInfo CreateStartInfo(ControlledCommand command)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var outputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        var info = new ProcessStartInfo { FileName = command.Executable, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = outputEncoding, StandardErrorEncoding = outputEncoding };
        foreach (var argument in command.Arguments) info.ArgumentList.Add(argument);
        return info;
    }

    private static async Task ReadLinesAsync(StreamReader reader, StringBuilder target, IProgress<string>? progress, string prefix)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            target.AppendLine(line);
            progress?.Report(prefix + line);
        }
    }

    private static void Validate(string command, IReadOnlyList<string> arguments)
    {
        if (arguments.Any(value => value.Length > 255)) throw new ArgumentException("单个命令参数过长。");
        if (command == "route" && (arguments.Count == 0 || !arguments[0].Equals("print", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("route 仅允许执行 route print。");
        if (command == "arp" && (arguments.Count == 0 || !arguments[0].Equals("-a", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("arp 仅允许执行 arp -a。");
        if (command == "ipconfig" && arguments.Any(value => !value.Equals("/all", StringComparison.OrdinalIgnoreCase) && !value.Equals("/displaydns", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("ipconfig 仅允许无参数、/all 或 /displaydns。");
        if (command == "nslookup" && arguments.Count > 2) throw new ArgumentException("nslookup 最多允许目标和 DNS 服务器两个参数。");
    }

    private static IReadOnlyList<string> Tokenize(string input)
    {
        var matches = TokenRegex().Matches(input);
        var tokens = matches.Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value).ToArray();
        if (string.Concat(matches.Select(match => match.Value)).Replace(" ", string.Empty).Length != input.Replace(" ", string.Empty).Length) throw new ArgumentException("命令参数格式无效。");
        return tokens;
    }

    [GeneratedRegex("[&|;<>`\\r\\n]")]
    private static partial Regex UnsafeCharacterRegex();
    [GeneratedRegex("\\\"([^\\\"]*)\\\"|(\\S+)")]
    private static partial Regex TokenRegex();
}
