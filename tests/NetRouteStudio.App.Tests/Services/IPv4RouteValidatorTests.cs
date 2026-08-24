using FluentAssertions;
using NetRouteStudio.App.Models;
using NetRouteStudio.App.Services;

namespace NetRouteStudio.App.Tests.Services;

public sealed class IPv4RouteValidatorTests
{
    [Fact]
    public void 校验无网关路由_应转换为OnLink下一跳()
    {
        var request = new IPv4RouteRequest("10.20.0.0/16", string.Empty, 7, 25, false);

        var normalized = IPv4RouteValidator.ValidateAndNormalize(request);

        normalized.DestinationPrefix.Should().Be("10.20.0.0/16");
        normalized.NextHop.Should().Be("0.0.0.0");
    }

    [Theory]
    [InlineData("10.20.1.1/16")]
    [InlineData("10.20.0.0/33")]
    [InlineData("2001:db8::/32")]
    [InlineData("not-cidr")]
    public void 校验非法或非规范CIDR_应拒绝(string prefix)
    {
        var request = new IPv4RouteRequest(prefix, "192.168.1.1", 7, 25, false);

        var action = () => IPv4RouteValidator.ValidateAndNormalize(request);

        action.Should().Throw<ArgumentException>();
    }
}
