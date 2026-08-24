namespace NetRouteStudio.App.Models;

public sealed record RouteInputMatchResult(
    string Input,
    bool IsDomain,
    IReadOnlyList<RouteMatchResult> Matches);
