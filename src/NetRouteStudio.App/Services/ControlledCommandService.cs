using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;
using System.ComponentModel;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed partial class ControlledCommandService : IControlledCommandService
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase) { "ping", "tracert", "pathping", "ipconfig", "route", "arp", "nslookup", "netstat", "getmac", "hostname", "nbtstat", "netsh", "telnet" };
    private static readonly HashSet<string> BlockedHosts = new(StringComparer.OrdinalIgnoreCase) { "cmd", "cmd.exe", "powershell", "powershell.exe", "pwsh", "pwsh.exe", "wscript", "wscript.exe", "cscript", "cscript.exe", "mshta", "mshta.exe", "rundll32", "rundll32.exe" };
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    public IReadOnlyList<ControlledCommandExample> Examples { get; } =
    [
        new("连通性", "测试本机 IPv4", "ping -n 4 127.0.0.1"),
        new("连通性", "测试域名连通性", "ping -n 4 www.baidu.com"),
        new("路径", "不解析主机名的路由跟踪", "tracert -d www.baidu.com"),
        new("路径", "分析路径和节点丢包", "pathping -n www.baidu.com"),
        new("IP 配置", "显示基本 IP 配置", "ipconfig"),
        new("IP 配置", "显示完整 IP 配置", "ipconfig /all"),
        new("DNS", "显示本机 DNS 缓存", "ipconfig /displaydns"),
        new("DNS", "使用默认 DNS 查询", "nslookup www.baidu.com"),
        new("DNS", "使用指定 DNS 查询", "nslookup www.baidu.com 223.5.5.5"),
        new("路由", "显示全部路由", "route print"),
        new("路由", "仅显示 IPv4 路由", "route print -4"),
        new("路由", "仅显示 IPv6 路由", "route print -6"),
        new("邻居", "显示 ARP 缓存", "arp -a"),
        new("连接", "显示连接、端口和 PID", "netstat -ano"),
        new("连接", "显示数字格式路由表", "netstat -rn"),
        new("网卡", "显示 MAC 地址", "getmac /v"),
        new("主机", "显示计算机主机名", "hostname"),
        new("NetBIOS", "显示本机名称表", "nbtstat -n"),
        new("NetBIOS", "显示 NetBIOS 统计", "nbtstat -r"),
        new("Netsh", "显示 IPv4 接口配置", "netsh interface ipv4 show config"),
        new("Netsh", "显示 IPv6 接口", "netsh interface ipv6 show interfaces"),
        new("无线", "显示无线网卡状态", "netsh wlan show interfaces"),
        new("无线", "显示附近无线网络", "netsh wlan show networks"),
        new("端口", "使用 Telnet 测试目标端口", "telnet 192.168.1.1 80")
    ];

    public ControlledCommand Parse(string input, bool whitelistEnabled = true)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ArgumentException("请输入要执行的白名单网络命令。");
        if (UnsafeCharacterRegex().IsMatch(input)) throw new ArgumentException("命令包含连接符、重定向符或换行，已拒绝执行。");
        var tokens = Tokenize(input);
        if (tokens.Count == 0) throw new ArgumentException("请输入要执行的命令。");
        if (!ExecutableNameRegex().IsMatch(tokens[0]) || BlockedHosts.Contains(tokens[0])) throw new ArgumentException("不允许启动命令解释器、脚本宿主或包含路径的程序。");
        if (whitelistEnabled && !Allowed.Contains(tokens[0])) throw new ArgumentException("该命令不在允许执行的网络命令白名单中。");
        var command = tokens[0].ToLowerInvariant();
        var arguments = tokens.Skip(1).ToArray();
        if (whitelistEnabled) Validate(command, arguments);
        return new ControlledCommand(string.Join(" ", tokens), command.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? command : command + ".exe", arguments);
    }

    public async Task<ControlledCommandResult> ExecuteAsync(string input, bool whitelistEnabled = true, IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default)
    {
        var command = Parse(input, whitelistEnabled);
        var started = DateTimeOffset.Now;
        using var process = new Process { StartInfo = CreateStartInfo(command) };
        try
        {
            if (!process.Start()) throw new InvalidOperationException($"无法启动 {command.Executable}。");
        }
        catch (Win32Exception exception) when (command.Executable.Equals("telnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("系统未安装 Telnet Client，请在 Windows 可选功能中启用后重试。", exception);
        }
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
        if (command == "hostname" && arguments.Count != 0) throw new ArgumentException("hostname 不允许附加参数。");
        if (command == "getmac" && arguments.Any(value => !value.Equals("/v", StringComparison.OrdinalIgnoreCase) && !value.Equals("/fo", StringComparison.OrdinalIgnoreCase) && !value.Equals("list", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("getmac 仅允许只读显示参数。");
        if (command == "nbtstat" && arguments.Any(value => !new[] { "-a", "-A", "-c", "-n", "-r", "-s", "-S" }.Contains(value, StringComparer.Ordinal))) throw new ArgumentException("nbtstat 仅允许只读查询参数。");
        if (command == "netsh" && !IsAllowedNetsh(arguments)) throw new ArgumentException("netsh 仅允许预设的 show 查询命令。");
        if (command == "telnet") ValidateTelnet(arguments);
    }

    private static void ValidateTelnet(IReadOnlyList<string> arguments)
    {
        if (arguments.Count is < 1 or > 2 || !HostRegex().IsMatch(arguments[0])) throw new ArgumentException("telnet 仅允许目标主机和可选端口，例如 telnet 192.168.1.1 80。");
        if (arguments.Count == 2 && (!int.TryParse(arguments[1], out var port) || port is < 1 or > 65535)) throw new ArgumentException("Telnet 端口必须为 1–65535。");
    }

    private static bool IsAllowedNetsh(IReadOnlyList<string> arguments)
    {
        var value = string.Join(" ", arguments).ToLowerInvariant();
        return value is "interface ipv4 show config" or "interface ipv6 show interfaces" or "wlan show interfaces" or "wlan show networks" or "wlan show drivers";
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
    [GeneratedRegex("^[a-zA-Z0-9_.-]+$")]
    private static partial Regex ExecutableNameRegex();
    [GeneratedRegex("^[a-zA-Z0-9_.:-]+$")]
    private static partial Regex HostRegex();
}
