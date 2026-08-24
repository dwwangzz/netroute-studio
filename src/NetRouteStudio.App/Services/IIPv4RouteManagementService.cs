using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IIPv4RouteManagementService
{
    string GetCreateCommand(IPv4RouteRequest request);

    string GetUpdateCommand(RouteInfo existingRoute, IPv4RouteRequest request);

    string GetDeleteCommand(RouteInfo route);

    Task<RouteMutationResult> CreateAsync(IPv4RouteRequest request, CancellationToken cancellationToken = default);

    Task<RouteMutationResult> UpdateAsync(
        RouteInfo existingRoute,
        IPv4RouteRequest request,
        CancellationToken cancellationToken = default);

    Task<RouteMutationResult> DeleteAsync(RouteInfo route, CancellationToken cancellationToken = default);
}
