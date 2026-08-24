using System.Net;
using System.Net.NetworkInformation;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class NetworkProbe : INetworkProbe
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken = default) =>
        await Dns.GetHostAddressesAsync(host, cancellationToken);

    public async Task<NetworkProbeReply> PingAsync(IPAddress address, int timeoutMilliseconds, int timeToLive, CancellationToken cancellationToken = default)
    {
        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(address, timeoutMilliseconds, new byte[32], new PingOptions(timeToLive, true)).WaitAsync(cancellationToken);
            return new NetworkProbeReply(reply.Status.ToString(), reply.Status is IPStatus.Success or IPStatus.TtlExpired ? reply.RoundtripTime : null,
                reply.Address?.ToString(), reply.Options?.Ttl, reply.Status == IPStatus.Success ? string.Empty : GetStatusMessage(reply.Status));
        }
        catch (PingException exception)
        {
            return new NetworkProbeReply("Error", null, null, null, exception.InnerException?.Message ?? exception.Message);
        }
    }

    private static string GetStatusMessage(IPStatus status) => status switch
    {
        IPStatus.TimedOut => "请求超时",
        IPStatus.TtlExpired => "TTL 已到期",
        IPStatus.DestinationHostUnreachable => "目标主机不可达",
        IPStatus.DestinationNetworkUnreachable => "目标网络不可达",
        _ => status.ToString()
    };
}
