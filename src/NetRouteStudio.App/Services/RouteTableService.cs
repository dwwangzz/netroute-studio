using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;

namespace NetRouteStudio.App.Services;

public sealed class RouteTableService(IPowerShellExecutor powerShellExecutor) : IRouteTableService
{
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(20);

    private const string ReadRoutesCommand = """
        function Get-RouteKey($route) {
            return "$([string]$route.AddressFamily)|$([int]$route.InterfaceIndex)|$([string]$route.DestinationPrefix)|$([string]$route.NextHop)"
        }

        $interfaceMetrics = @{}
        @(Get-NetIPInterface -IncludeAllCompartments) | ForEach-Object {
            $key = "$([string]$_.AddressFamily)|$([int]$_.InterfaceIndex)"
            $interfaceMetrics[$key] = [int]$_.InterfaceMetric
        }

        $activeRoutes = @{}
        @(Get-NetRoute -PolicyStore ActiveStore) | ForEach-Object {
            $activeRoutes[(Get-RouteKey $_)] = $_
        }

        $persistentRoutes = @{}
        @(Get-NetRoute -PolicyStore PersistentStore) | ForEach-Object {
            $persistentRoutes[(Get-RouteKey $_)] = $_
        }

        function Convert-Route($route, [bool]$isActive, [bool]$isPersistent) {
            $family = [string]$route.AddressFamily
            $interfaceKey = "$family|$([int]$route.InterfaceIndex)"
            $protocol = [string]$route.Protocol

            [pscustomobject]@{
                AddressFamily    = $family
                DestinationPrefix = [string]$route.DestinationPrefix
                NextHop          = [string]$route.NextHop
                InterfaceAlias   = [string]$route.InterfaceAlias
                InterfaceIndex   = [int]$route.InterfaceIndex
                RouteMetric      = [int]$route.RouteMetric
                InterfaceMetric  = [int]$interfaceMetrics[$interfaceKey]
                Protocol         = $protocol
                IsPersistent     = $isPersistent
                IsActive         = $isActive
                IsUserOperable   = [bool]($protocol -in @('NetMgmt', 'Static'))
            }
        }

        $items = @()
        foreach ($entry in $activeRoutes.GetEnumerator()) {
            $items += Convert-Route $entry.Value $true ($persistentRoutes.ContainsKey($entry.Key))
        }
        foreach ($entry in $persistentRoutes.GetEnumerator()) {
            if (-not $activeRoutes.ContainsKey($entry.Key)) {
                $items += Convert-Route $entry.Value $false $true
            }
        }

        [pscustomobject]@{ Items = $items }
        """;

    public async Task<IReadOnlyList<RouteInfo>> GetRoutesAsync(CancellationToken cancellationToken = default)
    {
        var result = await powerShellExecutor.ExecuteAsync<RouteEnvelope>(
            ReadRoutesCommand,
            ReadTimeout,
            cancellationToken);

        return result.Items
            .Select(MapRoute)
            .OrderBy(route => route.AddressFamily)
            .ThenBy(route => route.DestinationPrefix, StringComparer.OrdinalIgnoreCase)
            .ThenBy(route => route.EffectiveMetric)
            .ToArray();
    }

    private static RouteInfo MapRoute(RouteData route) => new(
        string.Equals(route.AddressFamily, "IPv6", StringComparison.OrdinalIgnoreCase)
            ? RouteAddressFamily.IPv6
            : RouteAddressFamily.IPv4,
        route.DestinationPrefix,
        route.NextHop,
        !route.IsActive && route.InterfaceIndex == 0
            ? "未绑定（选择接口后生效）"
            : route.InterfaceAlias,
        route.InterfaceIndex,
        route.RouteMetric,
        route.InterfaceMetric,
        route.Protocol,
        route.IsPersistent,
        route.IsUserOperable)
        {
            IsActive = route.IsActive
        };

    private sealed class RouteEnvelope
    {
        public RouteData[] Items { get; init; } = [];
    }

    private sealed class RouteData
    {
        public string AddressFamily { get; init; } = string.Empty;
        public string DestinationPrefix { get; init; } = string.Empty;
        public string NextHop { get; init; } = string.Empty;
        public string InterfaceAlias { get; init; } = string.Empty;
        public int InterfaceIndex { get; init; }
        public int RouteMetric { get; init; }
        public int InterfaceMetric { get; init; }
        public string Protocol { get; init; } = string.Empty;
        public bool IsPersistent { get; init; }
        public bool IsActive { get; init; }
        public bool IsUserOperable { get; init; }
    }
}
