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
    public string AddressFamilyDisplay => AddressFamily.ToString();

    public string LifetimeDisplay => IsPersistent ? "永久" : "临时";

    public string OperabilityDisplay => IsUserOperable ? "用户可操作" : "系统管理";

    public int EffectiveMetric => RouteMetric + InterfaceMetric;
}
