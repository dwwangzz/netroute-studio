using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IIPv6InterfaceMetricService
{
    string GetUpdateCommand(IPv6InterfaceMetricRequest request);

    Task<InterfaceMetricMutationResult> UpdateAsync(
        IPv6InterfaceMetricRequest request,
        CancellationToken cancellationToken = default);
}
