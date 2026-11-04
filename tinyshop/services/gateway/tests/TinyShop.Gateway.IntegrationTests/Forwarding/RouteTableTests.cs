using System.Net;
using System.Text;
using TinyShop.Gateway.IntegrationTests.Infrastructure;
using static TinyShop.Testing.ResponseAssertions;

namespace TinyShop.Gateway.IntegrationTests.Forwarding;

/// <summary>
/// The five public routes of contracts.md §4: each reaches exactly one upstream, at its internal
/// path, with the query string unchanged. Anything else is the gateway's own 404 or 405, and no
/// upstream is called.
/// </summary>
public sealed class RouteTableTests : GatewayWithUpstreams
{
    [Theory]
    [InlineData("GET", "/api/products", "product-service", "/products", "")]
    [InlineData("GET", "/api/users/42", "user-service", "/users/42", "")]
    [InlineData("POST", "/api/orders", "order-service", "/orders", "")]
    [InlineData("GET", "/api/orders/1001", "order-service", "/orders/1001", "")]
    [InlineData("GET", "/api/orders?userId=42", "order-service", "/orders", "?userId=42")]
    public async Task PublicRoute_IsForwardedToItsUpstreamOnly(
        string method, string publicUrl, string upstream, string internalPath, string query)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), publicUrl);
        if (method == "POST")
        {
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        await Client.SendAsync(request, TestContext.Current.CancellationToken);

        var target = Upstream(upstream);
        Assert.NotNull(target.LastRequest);
        Assert.Equal(method, target.LastRequest.Method);
        Assert.Equal(internalPath, target.LastRequest.Path);
        Assert.Equal(query, target.LastRequest.QueryString);
        foreach (var other in new[] { ProductService, UserService, OrderService }.Where(u => u != target))
        {
            Assert.Null(other.LastRequest);
        }
    }

    [Fact]
    public async Task QueryString_IsForwardedExactlyAsSent()
    {
        await Client.GetAsync("/api/products?sort=name&page=2&q=caf%C3%A9", TestContext.Current.CancellationToken);

        Assert.Equal("?sort=name&page=2&q=caf%C3%A9", ProductService.LastRequest?.QueryString);
    }

    [Fact]
    public async Task ApiVersion_IsTheUpstreamsBusiness_NotTheGateways()
    {
        // The shared setup registers API versioning, which answers 400 unsupported_api_version for
        // versions it doesn't know. The gateway's routes have no version: the query and the header
        // must reach the service, which decides (TS-014, technical notes).
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products?api-version=9.0");
        request.Headers.Add("api-version", "9.0");

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("?api-version=9.0", ProductService.LastRequest?.QueryString);
        Assert.Equal("9.0", ProductService.LastRequest?.Headers["api-version"]);
    }

    [Theory]
    [InlineData("POST", "/api/products", "GET")]
    [InlineData("DELETE", "/api/orders/1001", "GET")]
    [InlineData("PUT", "/api/users/42", "GET")]
    [InlineData("DELETE", "/api/orders", "GET, POST")]   // two routes share the path
    public async Task WrongMethod_Returns405MethodNotAllowed(string method, string path, string allow)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);

        var response = await Client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed", path);
        // RFC 9110 §15.5.6: a 405 must say which methods the path does accept (contracts.md §4).
        // In .NET, Allow is a content header.
        Assert.Equal(allow, string.Join(", ", response.Content.Headers.Allow.Order(StringComparer.Ordinal)));
        AssertNoUpstreamWasCalled();
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/users")]                // only /api/users/{id} exists
    [InlineData("/api/orders/1001/items")]
    [InlineData("/users/42")]                 // an internal path
    public async Task PathOutsideTheTable_Returns404NotFound(string path)
    {
        var response = await Client.GetAsync(path, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found", path);
        AssertNoUpstreamWasCalled();
    }
}
