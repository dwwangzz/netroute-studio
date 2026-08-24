namespace NetRouteStudio.App.Models;

public sealed record NativeRouteMatch(
    string DestinationPrefix,
    string NextHop,
    string InterfaceAlias,
    int InterfaceIndex,
    int RouteMetric,
    int InterfaceMetric)
{
    public bool IsAvailable { get; init; } = true;

    public string ErrorMessage { get; init; } = string.Empty;
}
