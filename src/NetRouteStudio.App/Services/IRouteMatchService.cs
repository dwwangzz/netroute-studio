using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IRouteMatchService
{
    Task<RouteMatchResult> MatchAsync(string targetAddress, CancellationToken cancellationToken = default);

    Task<RouteInputMatchResult> MatchInputAsync(string input, CancellationToken cancellationToken = default);
}
