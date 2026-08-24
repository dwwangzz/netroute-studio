using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface INetworkTestService
{
    Task<NetworkTestResult> TestAsync(string input, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}
