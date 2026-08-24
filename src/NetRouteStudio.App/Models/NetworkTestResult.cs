namespace NetRouteStudio.App.Models;

public sealed record NetworkTestResult(
    string Input,
    bool IsDomain,
    IReadOnlyList<string> ResolvedAddresses,
    IReadOnlyList<NetworkPingResult> PingResults,
    IReadOnlyList<TraceRouteHop> TraceHops,
    IReadOnlyList<RouteMatchResult> RouteMatches,
    string Summary);
