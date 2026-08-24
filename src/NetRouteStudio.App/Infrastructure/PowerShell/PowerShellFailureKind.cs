namespace NetRouteStudio.App.Infrastructure.PowerShell;

public enum PowerShellFailureKind
{
    CommandFailed,
    Timeout,
    EmptyOutput,
    InvalidJson
}
