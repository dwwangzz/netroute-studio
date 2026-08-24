namespace NetRouteStudio.App.Models;

public sealed record RouteMatchResult(
    string TargetAddress,
    IReadOnlyList<RouteCandidate> Candidates,
    RouteInfo? MatchedRoute,
    NativeRouteMatch NativeRoute,
    bool IsNativeMatch,
    string DecisionReason);
