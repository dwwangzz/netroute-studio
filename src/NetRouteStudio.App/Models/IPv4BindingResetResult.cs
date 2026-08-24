namespace NetRouteStudio.App.Models;

public sealed record IPv4BindingResetResult(
    IPv4BindingInfo Before,
    IPv4BindingInfo After,
    NetworkAdapterInfo VerifiedAdapter,
    bool EnableRetried);
