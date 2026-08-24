namespace NetRouteStudio.App.Models;

public sealed record RouteInfo(
    RouteAddressFamily AddressFamily,
    string DestinationPrefix,
    string NextHop,
    string InterfaceAlias,
    int InterfaceIndex,
    int RouteMetric,
    int InterfaceMetric,
    string Protocol,
    bool IsPersistent,
    bool IsUserOperable)
{
    public bool IsActive { get; init; } = true;

    public string AddressFamilyDisplay => AddressFamily.ToString();

    public string LifetimeDisplay => IsPersistent
        ? IsActive ? "永久（已生效）" : "永久（未生效）"
        : "临时";

    public string OperabilityDisplay => IsUserOperable ? "用户可操作" : "系统管理";

    public int EffectiveMetric => RouteMetric + InterfaceMetric;
}
