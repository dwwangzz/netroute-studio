namespace NetRouteStudio.App.Models;

public sealed record IPv4InterfaceMetricRequest(
    int InterfaceIndex,
    bool AutomaticMetric,
    int? InterfaceMetric);
