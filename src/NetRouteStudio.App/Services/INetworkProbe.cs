using System.Net;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface INetworkProbe
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default);
    Task<NetworkProbeReply> PingAsync(IPAddress address, int timeoutMilliseconds, int timeToLive, CancellationToken cancellationToken = default);
}
