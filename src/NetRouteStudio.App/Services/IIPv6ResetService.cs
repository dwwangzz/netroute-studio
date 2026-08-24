using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IIPv6ResetService
{
    string GetResetCommand(string adapterName);

    string GetManualEnableCommand(string adapterName);

    Task<IReadOnlyList<IPv6BindingInfo>> GetBindingsAsync(CancellationToken cancellationToken = default);

    Task<IPv6BindingInfo> GetBindingAsync(string adapterName, CancellationToken cancellationToken = default);

    Task<IPv6ResetResult> ResetAsync(
        NetworkAdapterInfo adapter,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
