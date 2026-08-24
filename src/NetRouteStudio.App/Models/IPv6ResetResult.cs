namespace NetRouteStudio.App.Models;

public sealed record IPv6ResetResult(
    IPv6BindingInfo Before,
    IPv6BindingInfo After,
    NetworkAdapterInfo VerifiedAdapter,
    bool EnableRetried);
