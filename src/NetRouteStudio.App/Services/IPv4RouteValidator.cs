using System.Net;
using System.Net.Sockets;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public static class IPv4RouteValidator
{
    public static IPv4RouteRequest ValidateAndNormalize(IPv4RouteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var prefixParts = request.DestinationPrefix.Trim().Split('/', 2);
        if (prefixParts.Length != 2 ||
            !IPAddress.TryParse(prefixParts[0], out var address) ||
            address.AddressFamily != AddressFamily.InterNetwork ||
            !int.TryParse(prefixParts[1], out var prefixLength) ||
            prefixLength is < 0 or > 32)
        {
            throw new ArgumentException("目标必须是有效的 IPv4 CIDR，例如 10.20.0.0/16。");
        }

        var networkAddress = GetNetworkAddress(address, prefixLength);
        if (!address.Equals(networkAddress))
        {
            throw new ArgumentException($"目标 CIDR 不是规范网络地址，建议使用 {networkAddress}/{prefixLength}。");
        }

        var nextHopText = string.IsNullOrWhiteSpace(request.NextHop)
            ? "0.0.0.0"
            : request.NextHop.Trim();
        if (!IPAddress.TryParse(nextHopText, out var nextHop) ||
            nextHop.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException("下一跳必须为空或有效的 IPv4 地址。");
        }

        if (request.InterfaceIndex <= 0)
        {
            throw new ArgumentException("接口索引必须大于零。");
        }

        if (request.RouteMetric is < 0 or > 9999)
        {
            throw new ArgumentException("路由跃点必须在 0 到 9999 之间。");
        }

        return request with
        {
            DestinationPrefix = $"{networkAddress}/{prefixLength}",
            NextHop = nextHop.ToString()
        };
    }

    private static IPAddress GetNetworkAddress(IPAddress address, int prefixLength)
    {
        var bytes = address.GetAddressBytes();
        var wholeBytes = prefixLength / 8;
        var remainingBits = prefixLength % 8;
        if (wholeBytes < bytes.Length)
        {
            if (remainingBits > 0)
            {
                bytes[wholeBytes] &= (byte)(0xFF << (8 - remainingBits));
                wholeBytes++;
            }

            Array.Clear(bytes, wholeBytes, bytes.Length - wholeBytes);
        }

        return new IPAddress(bytes);
    }
}
