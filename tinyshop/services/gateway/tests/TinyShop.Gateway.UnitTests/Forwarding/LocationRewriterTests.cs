using TinyShop.Gateway.Api.Forwarding;

namespace TinyShop.Gateway.UnitTests.Forwarding;

/// <summary>
/// Location, from an internal path to the public one (contracts.md §2.4 rule 3). Only a path on
/// the same host is rewritten; anything else passes through as the service sent it.
/// </summary>
public class LocationRewriterTests
{
    [Theory]
    [InlineData("/orders/1001", "/api/orders/1001")]
    [InlineData("/orders/1001?view=full", "/api/orders/1001?view=full")]
    [InlineData("/users/42", "/api/users/42")]
    public void InternalPath_BecomesThePublicPath(string location, string expected)
    {
        Assert.Equal(expected, LocationRewriter.ToPublic(location));
    }

    [Theory]
    [InlineData("/api/orders/1001")]               // already public: never /api/api/...
    [InlineData("https://example.com/orders/1")]   // another host
    [InlineData("//example.com/orders/1")]         // also another host ("scheme-relative")
    [InlineData("orders/1001")]                    // relative to the request, not to the root
    public void AnythingElse_IsUnchanged(string location)
    {
        Assert.Equal(location, LocationRewriter.ToPublic(location));
    }
}
