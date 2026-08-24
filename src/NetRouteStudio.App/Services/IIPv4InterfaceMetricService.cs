using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IIPv4InterfaceMetricService
{
    string GetUpdateCommand(IPv4InterfaceMetricRequest request);

    Task<InterfaceMetricMutationResult> UpdateAsync(
        IPv4InterfaceMetricRequest request,
        CancellationToken cancellationToken = default);
}
