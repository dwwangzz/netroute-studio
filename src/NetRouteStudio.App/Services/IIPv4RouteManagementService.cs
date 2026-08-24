using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IIPv4RouteManagementService
{
    Task<RouteMutationResult> CreateAsync(IPv4RouteRequest request, CancellationToken cancellationToken = default);

    Task<RouteMutationResult> UpdateAsync(
        RouteInfo existingRoute,
        IPv4RouteRequest request,
        CancellationToken cancellationToken = default);

    Task<RouteMutationResult> DeleteAsync(RouteInfo route, CancellationToken cancellationToken = default);
}
