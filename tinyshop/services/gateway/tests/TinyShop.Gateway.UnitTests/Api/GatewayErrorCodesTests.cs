using TinyShop.Gateway.Api.Errors;
using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.Gateway.UnitTests.Api;

/// <summary>
/// Only what the gateway adds to the shared error catalog. The customizer itself is tested once,
/// in TinyShop.ServiceDefaults.UnitTests.
/// </summary>
public class GatewayErrorCodesTests
{
    [Theory]
    [InlineData(GatewayErrorCodes.UpstreamUnreachable, "Upstream unreachable")]
    [InlineData(GatewayErrorCodes.UpstreamTimeout, "Upstream timeout")]
    public void Register_AddsEachCodeWithItsTitle(string code, string title)
    {
        Assert.Equal(title, Catalog().TitleFor(code));
    }

    [Theory]
    [InlineData(502, "upstream_unreachable")]
    [InlineData(504, "upstream_timeout")]
    public void GatewayGeneratedStatus_GetsTheGatewaysCode(int status, string expectedCode)
    {
        Assert.Equal(expectedCode, Catalog().CodeForStatus(status));
    }

    private static ErrorCatalog Catalog()
    {
        var catalog = new ErrorCatalog();
        GatewayErrorCodes.Register(catalog);
        return catalog;
    }
}
