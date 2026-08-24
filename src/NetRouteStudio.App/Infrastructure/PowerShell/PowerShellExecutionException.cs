namespace NetRouteStudio.App.Infrastructure.PowerShell;

public sealed class PowerShellExecutionException : Exception
{
    public PowerShellExecutionException(
        PowerShellFailureKind failureKind,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }

    public PowerShellFailureKind FailureKind { get; }
}
