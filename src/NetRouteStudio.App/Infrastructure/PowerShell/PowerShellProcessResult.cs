namespace NetRouteStudio.App.Infrastructure.PowerShell;

public sealed record PowerShellProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut);
