using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface INetworkAdapterService
{
    Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(
        CancellationToken cancellationToken = default);
}
