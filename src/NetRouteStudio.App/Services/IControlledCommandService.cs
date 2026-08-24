using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IControlledCommandService
{
    ControlledCommand Parse(string input);
    Task<ControlledCommandResult> ExecuteAsync(string input, IProgress<string>? outputProgress = null, CancellationToken cancellationToken = default);
}
