using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IIPv4BindingResetService
{
    string GetResetCommand(string adapterName);
    string GetManualEnableCommand(string adapterName);
    Task<IReadOnlyList<IPv4BindingInfo>> GetBindingsAsync(CancellationToken cancellationToken = default);
    Task<IPv4BindingInfo> GetBindingAsync(string adapterName, CancellationToken cancellationToken = default);
    Task<IPv4BindingResetResult> ResetAsync(
        NetworkAdapterInfo adapter,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}
