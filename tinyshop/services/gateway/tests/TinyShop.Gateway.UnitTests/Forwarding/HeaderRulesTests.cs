using TinyShop.Gateway.Api.Forwarding;

namespace TinyShop.Gateway.UnitTests.Forwarding;

/// <summary>
/// Hop-by-hop headers (RFC 9110 §7.6.1): the fixed list, plus whatever the message's own
/// Connection header names. Header names are case-insensitive.
/// </summary>
public class HeaderRulesTests
{
    private static readonly string[] NoConnectionHeader = [];

    [Theory]
    [InlineData("Connection")]
    [InlineData("Keep-Alive")]
    [InlineData("Transfer-Encoding")]
    [InlineData("TE")]
    [InlineData("Trailer")]
    [InlineData("Upgrade")]
    [InlineData("Proxy-Authenticate")]
    [InlineData("Proxy-Authorization")]
    [InlineData("transfer-encoding")]
    [InlineData("KEEP-ALIVE")]
    public void FixedHopByHopHeader_IsHopByHop(string name)
    {
        Assert.True(HeaderRules.IsHopByHop(name, NoConnectionHeader));
    }

    [Theory]
    [InlineData("Content-Type")]
    [InlineData("Content-Length")]
    [InlineData("Authorization")]
    [InlineData("Location")]
    [InlineData("X-Request-Id")]
    public void EndToEndHeader_IsNotHopByHop(string name)
    {
        Assert.False(HeaderRules.IsHopByHop(name, NoConnectionHeader));
    }

    [Theory]
    [InlineData("X-Hop", new[] { "X-Hop" })]
    [InlineData("X-Hop", new[] { "keep-alive, X-Hop" })]       // one value holding a comma-separated list
    [InlineData("X-Hop", new[] { "keep-alive", " x-hop " })]   // several values; spaces and case don't matter
    public void HeaderNamedInConnection_IsHopByHop(string name, string[] connection)
    {
        Assert.True(HeaderRules.IsHopByHop(name, connection));
    }

    [Fact]
    public void HeaderNotNamedInConnection_IsNotHopByHop()
    {
        Assert.False(HeaderRules.IsHopByHop("X-Other", ["keep-alive, X-Hop"]));
    }
}
