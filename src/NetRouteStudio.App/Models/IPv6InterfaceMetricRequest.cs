namespace NetRouteStudio.App.Models;

public sealed record IPv6InterfaceMetricRequest(
    int InterfaceIndex,
    bool AutomaticMetric,
    int? InterfaceMetric);
