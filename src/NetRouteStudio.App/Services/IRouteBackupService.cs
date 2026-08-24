using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IRouteBackupService
{
    Task<RouteBackupResult> CreateAsync(string filePath, CancellationToken cancellationToken = default);

    Task<NetworkBackupDocument> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}
