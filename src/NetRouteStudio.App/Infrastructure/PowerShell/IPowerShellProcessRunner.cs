namespace NetRouteStudio.App.Infrastructure.PowerShell;

public interface IPowerShellProcessRunner
{
    Task<PowerShellProcessResult> RunAsync(
        string encodedCommand,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
