namespace NetRouteStudio.App.Models;

public sealed record IPv4RouteRequest(
    string DestinationPrefix,
    string NextHop,
    int InterfaceIndex,
    int RouteMetric,
    bool IsPersistent);
