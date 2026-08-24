using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IControlledCommandService
{
    IReadOnlyList<ControlledCommandExample> Examples { get; }
    ControlledCommand Parse(string input, bool whitelistEnabled = true);
    Task<ControlledCommandResult> ExecuteAsync(string input, bool whitelistEnabled = true, IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default);
}
