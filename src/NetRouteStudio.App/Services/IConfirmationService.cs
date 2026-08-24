using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public interface IConfirmationService
{
    bool Confirm(RouteConfirmationRequest request);
}
