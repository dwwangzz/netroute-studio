using System.Text.Json;
using FluentAssertions;
using NetRouteStudio.App.Infrastructure.PowerShell;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class NetworkAdapterServiceTests
{
    [Fact]
    public async Task 获取网卡_应组合读取命令并映射结构化字段()
    {
        const string json = """
            {
              "Items": [
                {
                  "Name": "Ethernet 1",
                  "InterfaceDescription": "Intel Ethernet",
                  "InterfaceIndex": 7,
                  "Status": "Up",
                  "MacAddress": "00-11-22-33-44-55",
                  "LinkSpeed": "1 Gbps",
                  "HardwareInterface": true,
                  "Virtual": false,
                  "IPv4Addresses": ["192.168.1.10/24"],
                  "IPv6Addresses": [
                    "2408::10/64",
                    "2408::20/64（临时）",
                    "fe80::1/64（本地链接）"
                  ],
                  "DnsServers": ["1.1.1.1"],
                  "Gateways": ["192.168.1.1"],
                  "IPv4InterfaceMetric": 25,
                  "IPv4AutomaticMetric": true,
                  "IPv6InterfaceMetric": 35,
                  "IPv6AutomaticMetric": false
                }
              ]
            }
            """;
        var executor = new JsonStubPowerShellExecutor(json);
        var service = new NetworkAdapterService(executor);

        var adapters = await service.GetAdaptersAsync();

        adapters.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new NetworkAdapterInfo(
                "Ethernet 1",
                "Intel Ethernet",
                7,
                "Up",
                "00-11-22-33-44-55",
                "1 Gbps",
                NetworkAdapterKind.Physical,
                ["192.168.1.10/24"],
                ["2408::10/64", "2408::20/64（临时）", "fe80::1/64（本地链接）"],
                ["1.1.1.1"],
                ["192.168.1.1"],
                25,
                true,
                35,
                false));
        executor.Command.Should().Contain("Get-NetAdapter");
        executor.Command.Should().Contain("Get-NetIPConfiguration");
        executor.Command.Should().Contain("Get-NetIPAddress -AddressFamily IPv6");
        executor.Command.Should().Contain("Get-NetIPInterface");
        executor.Command.Should().Contain("[int]$_.InterfaceIndex");
        executor.Command.Should().Contain("[int]$adapter.InterfaceIndex");
    }

    [Fact]
    public async Task 获取网卡_Dns和网关为空_应返回空集合并识别虚拟网卡()
    {
        const string json = """
            {
              "Items": [
                {
                  "Name": "vEthernet (Default Switch)",
                  "InterfaceDescription": "Hyper-V Virtual Ethernet Adapter",
                  "InterfaceIndex": 18,
                  "Status": "Up",
                  "MacAddress": "AA-BB-CC-DD-EE-FF",
                  "LinkSpeed": "10 Gbps",
                  "HardwareInterface": false,
                  "Virtual": true,
                  "IPv4Addresses": [],
                  "IPv6Addresses": [],
                  "DnsServers": [],
                  "Gateways": [],
                  "IPv4InterfaceMetric": null,
                  "IPv4AutomaticMetric": null,
                  "IPv6InterfaceMetric": null,
                  "IPv6AutomaticMetric": null
                }
              ]
            }
            """;
        var service = new NetworkAdapterService(new JsonStubPowerShellExecutor(json));

        var adapter = (await service.GetAdaptersAsync()).Should().ContainSingle().Subject;

        adapter.Kind.Should().Be(NetworkAdapterKind.Virtual);
        adapter.DnsServers.Should().BeEmpty();
        adapter.Gateways.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task 获取真实Windows网卡_应返回合法结构化数据()
    {
        var service = new NetworkAdapterService(
            new PowerShellExecutor(new WindowsPowerShellProcessRunner()));

        var adapters = await service.GetAdaptersAsync();

        adapters.Should().NotBeEmpty();
        adapters.Should().OnlyContain(adapter =>
            !string.IsNullOrWhiteSpace(adapter.Name) && adapter.InterfaceIndex > 0);
        adapters.Should().Contain(adapter =>
            adapter.IPv4Addresses.Count > 0 ||
            adapter.IPv6Addresses.Count > 0 ||
            adapter.DnsServers.Count > 0 ||
            adapter.Gateways.Count > 0);
    }

    private sealed class JsonStubPowerShellExecutor(string json) : IPowerShellExecutor
    {
        public string? Command { get; private set; }

        public Task<T> ExecuteAsync<T>(
            string command,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Command = command;
            return Task.FromResult(JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!);
        }
    }
}
