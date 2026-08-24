using FluentAssertions;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class ControlledCommandServiceTests
{
    private readonly ControlledCommandService _service = new();

    [Theory]
    [InlineData("ping -n 2 example.com", "ping.exe")]
    [InlineData("tracert -d 192.0.2.1", "tracert.exe")]
    [InlineData("ipconfig /all", "ipconfig.exe")]
    [InlineData("route print -4", "route.exe")]
    [InlineData("arp -a", "arp.exe")]
    [InlineData("nslookup example.com 1.1.1.1", "nslookup.exe")]
    [InlineData("netstat -ano", "netstat.exe")]
    public void 白名单命令_应解析为固定可执行文件(string input, string executable) =>
        _service.Parse(input).Executable.Should().Be(executable);

    [Theory]
    [InlineData("cmd /c ping 127.0.0.1")]
    [InlineData("ping 127.0.0.1 & whoami")]
    [InlineData("ping 127.0.0.1 | more")]
    [InlineData("ipconfig > out.txt")]
    [InlineData("route add 10.0.0.0 mask 255.0.0.0 1.1.1.1")]
    [InlineData("arp -d *")]
    [InlineData("ipconfig /flushdns")]
    public void 非白名单或危险参数_应拒绝(string input) =>
        _service.Invoking(service => service.Parse(input)).Should().Throw<ArgumentException>();
}
