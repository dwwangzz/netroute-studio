using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class NetworkAdapterService(IPowerShellExecutor powerShellExecutor) : INetworkAdapterService
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);

    private const string ReadAdaptersCommand = """
        $configurations = @{}
        Get-NetIPConfiguration -All | ForEach-Object {
            $configurations[[int]$_.InterfaceIndex] = $_
        }

        $ipv6Addresses = @{}
        Get-NetIPAddress -AddressFamily IPv6 -IncludeAllCompartments -PolicyStore ActiveStore | ForEach-Object {
            $key = [int]$_.InterfaceIndex
            if (-not $ipv6Addresses.ContainsKey($key)) {
                $ipv6Addresses[$key] = @()
            }

            $label = if ($_.IPAddress -like 'fe80:*') {
                '（本地链接）'
            }
            elseif ([string]$_.SuffixOrigin -eq 'Random') {
                '（临时）'
            }
            else {
                ''
            }

            $ipv6Addresses[$key] = @($ipv6Addresses[$key]) + "$($_.IPAddress)/$($_.PrefixLength)$label"
        }

        $ipv4Interfaces = @{}
        Get-NetIPInterface -AddressFamily IPv4 -IncludeAllCompartments | ForEach-Object {
            $ipv4Interfaces[[int]$_.InterfaceIndex] = $_
        }

        $ipv6Interfaces = @{}
        Get-NetIPInterface -AddressFamily IPv6 -IncludeAllCompartments | ForEach-Object {
            $ipv6Interfaces[[int]$_.InterfaceIndex] = $_
        }

        $items = @(Get-NetAdapter -IncludeHidden | ForEach-Object {
            $adapter = $_
            $interfaceIndex = [int]$adapter.InterfaceIndex
            $configuration = $configurations[$interfaceIndex]
            $ipv4Interface = $ipv4Interfaces[$interfaceIndex]
            $ipv6Interface = $ipv6Interfaces[$interfaceIndex]

            [pscustomobject]@{
                Name                 = [string]$adapter.Name
                InterfaceDescription = [string]$adapter.InterfaceDescription
                InterfaceIndex       = [int]$adapter.InterfaceIndex
                Status               = [string]$adapter.Status
                MacAddress           = [string]$adapter.MacAddress
                LinkSpeed            = [string]$adapter.LinkSpeed
                HardwareInterface    = [bool]$adapter.HardwareInterface
                Virtual              = [bool]$adapter.Virtual
                IPv4Addresses        = @($configuration.IPv4Address | ForEach-Object {
                    "$($_.IPAddress)/$($_.PrefixLength)"
                })
                IPv6Addresses        = @($ipv6Addresses[$interfaceIndex] | Sort-Object -Unique)
                DnsServers           = @($configuration.DNSServer.ServerAddresses)
                Gateways             = @(@(
                    $configuration.IPv4DefaultGateway.NextHop
                    $configuration.IPv6DefaultGateway.NextHop
                ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
                IPv4InterfaceMetric  = $ipv4Interface.InterfaceMetric
                IPv4AutomaticMetric = if ($null -eq $ipv4Interface) { $null } else {
                    [string]$ipv4Interface.AutomaticMetric -eq 'Enabled'
                }
                IPv6InterfaceMetric  = $ipv6Interface.InterfaceMetric
                IPv6AutomaticMetric = if ($null -eq $ipv6Interface) { $null } else {
                    [string]$ipv6Interface.AutomaticMetric -eq 'Enabled'
                }
            }
        })

        [pscustomobject]@{ Items = $items }
        """;

    public async Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await powerShellExecutor.ExecuteAsync<NetworkAdapterEnvelope>(
            ReadAdaptersCommand,
            ReadTimeout,
            cancellationToken);

        return result.Items
            .Select(MapAdapter)
            .OrderBy(adapter => adapter.Kind)
            .ThenBy(adapter => adapter.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static NetworkAdapterInfo MapAdapter(NetworkAdapterData adapter) => new(
        adapter.Name,
        adapter.InterfaceDescription,
        adapter.InterfaceIndex,
        adapter.Status,
        adapter.MacAddress,
        adapter.LinkSpeed,
        GetKind(adapter),
        adapter.IPv4Addresses,
        adapter.IPv6Addresses,
        adapter.DnsServers,
        adapter.Gateways,
        adapter.IPv4InterfaceMetric,
        adapter.IPv4AutomaticMetric,
        adapter.IPv6InterfaceMetric,
        adapter.IPv6AutomaticMetric);

    private static NetworkAdapterKind GetKind(NetworkAdapterData adapter)
    {
        if (adapter.Virtual || !adapter.HardwareInterface)
        {
            return NetworkAdapterKind.Virtual;
        }

        return adapter.HardwareInterface
            ? NetworkAdapterKind.Physical
            : NetworkAdapterKind.Unknown;
    }

    private sealed class NetworkAdapterEnvelope
    {
        public NetworkAdapterData[] Items { get; init; } = [];
    }

    private sealed class NetworkAdapterData
    {
        public string Name { get; init; } = string.Empty;
        public string InterfaceDescription { get; init; } = string.Empty;
        public int InterfaceIndex { get; init; }
        public string Status { get; init; } = string.Empty;
        public string MacAddress { get; init; } = string.Empty;
        public string LinkSpeed { get; init; } = string.Empty;
        public bool HardwareInterface { get; init; }
        public bool Virtual { get; init; }
        public string[] IPv4Addresses { get; init; } = [];
        public string[] IPv6Addresses { get; init; } = [];
        public string[] DnsServers { get; init; } = [];
        public string[] Gateways { get; init; } = [];
        public int? IPv4InterfaceMetric { get; init; }
        public bool? IPv4AutomaticMetric { get; init; }
        public int? IPv6InterfaceMetric { get; init; }
        public bool? IPv6AutomaticMetric { get; init; }
    }
}
