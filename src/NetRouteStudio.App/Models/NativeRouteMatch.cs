namespace NetRouteStudio.App.Models;

public sealed record NativeRouteMatch(
    string DestinationPrefix,
    string NextHop,
    string InterfaceAlias,
    int InterfaceIndex,
    int RouteMetric,
    int InterfaceMetric);
