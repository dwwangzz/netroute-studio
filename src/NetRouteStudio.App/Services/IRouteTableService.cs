using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IRouteTableService
{
    Task<IReadOnlyList<RouteInfo>> GetRoutesAsync(CancellationToken cancellationToken = default);
}
