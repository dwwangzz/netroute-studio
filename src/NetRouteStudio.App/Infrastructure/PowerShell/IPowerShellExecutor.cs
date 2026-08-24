namespace NetRouteStudio.App.Infrastructure.PowerShell;

public interface IPowerShellExecutor
{
    Task<T> ExecuteAsync<T>(
        string command,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
