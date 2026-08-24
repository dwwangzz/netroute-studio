namespace NetRouteStudio.App.Models;

public sealed record NetworkBackupDocument(
    string FormatVersion,
    DateTimeOffset CreatedAt,
    string TimeZoneId,
    string ComputerName,
    string WindowsVersion,
    string AppVersion,
    int RouteCount,
    int AdapterCount,
    IReadOnlyList<RouteInfo> Routes,
    IReadOnlyList<NetworkAdapterInfo> Adapters,
    string Sha256);
