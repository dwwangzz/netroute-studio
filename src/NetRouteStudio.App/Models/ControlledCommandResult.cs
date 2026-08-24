namespace NetRouteStudio.App.Models;

public sealed record ControlledCommandResult(string Command, DateTimeOffset StartedAt, TimeSpan Duration, int ExitCode, string StandardOutput, string StandardError, bool TimedOut, bool Cancelled)
{
    public string StatusDisplay => Cancelled ? "已取消" : TimedOut ? "已超时" : ExitCode == 0 ? "成功" : $"失败（{ExitCode}）";
    public string Output => string.Join(Environment.NewLine, new[] { StandardOutput, StandardError }.Where(value => !string.IsNullOrWhiteSpace(value)));
}
