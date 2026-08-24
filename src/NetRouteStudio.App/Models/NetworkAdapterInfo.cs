namespace NetRouteStudio.App.Models;

public sealed record NetworkAdapterInfo(
    string Name,
    string InterfaceDescription,
    int InterfaceIndex,
    string Status,
    string MacAddress,
    string LinkSpeed,
    NetworkAdapterKind Kind,
    IReadOnlyList<string> IPv4Addresses,
    IReadOnlyList<string> IPv6Addresses,
    IReadOnlyList<string> DnsServers,
    IReadOnlyList<string> Gateways,
    int? IPv4InterfaceMetric,
    bool? IPv4AutomaticMetric,
    int? IPv6InterfaceMetric,
    bool? IPv6AutomaticMetric)
{
    public string KindDisplay => Kind switch
    {
        NetworkAdapterKind.Physical => "物理网卡",
        NetworkAdapterKind.Virtual => "虚拟网卡",
        _ => "未知类型"
    };

    public string IPv4Display => JoinOrEmpty(IPv4Addresses);

    public string IPv6Display => JoinOrEmpty(IPv6Addresses);

    public string DnsDisplay => JoinOrEmpty(DnsServers);

    public string GatewayDisplay => JoinOrEmpty(Gateways);

    public string SelectionDisplay =>
        $"{Name}｜索引 {InterfaceIndex}｜{(IPv4Addresses.Count > 0 ? IPv4Addresses[0] : "无 IPv4")}｜{Status}";

    public string IPv4MetricDisplay => FormatMetric(IPv4InterfaceMetric, IPv4AutomaticMetric);

    public string IPv6MetricDisplay => FormatMetric(IPv6InterfaceMetric, IPv6AutomaticMetric);

    private static string JoinOrEmpty(IReadOnlyList<string> values) =>
        values.Count == 0 ? "—" : string.Join(Environment.NewLine, values);

    private static string FormatMetric(int? metric, bool? automatic) => metric is null
        ? "—"
        : $"{metric}（{(automatic == true ? "自动" : "手动")}）";
}
