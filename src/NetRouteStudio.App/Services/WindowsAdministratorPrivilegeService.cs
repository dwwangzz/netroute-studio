using System.Security.Principal;

namespace NetRouteStudio.App.Services;

public sealed class WindowsAdministratorPrivilegeService : IAdministratorPrivilegeService
{
    public bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
