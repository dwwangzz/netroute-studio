namespace NetRouteStudio.App.Models;

public sealed record RouteCandidate(RouteInfo Route, int PrefixLength, string MatchReason);
