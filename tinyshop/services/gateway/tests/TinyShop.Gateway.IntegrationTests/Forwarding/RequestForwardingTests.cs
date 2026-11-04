using System.Net.Http.Headers;
using System.Text;
using TinyShop.Gateway.IntegrationTests.Infrastructure;

namespace TinyShop.Gateway.IntegrationTests.Forwarding;

/// <summary>
/// What the upstream receives: the client's body and end-to-end headers; never the hop-by-hop
/// headers of the client's connection; and its own Host (TS-014, decision 3).
/// </summary>
public sealed class RequestForwardingTests : GatewayWithUpstreams
{
    private const string OrderJson = """{"userId":42,"items":[{"productId":1,"quantity":2}]}""";

    [Fact]
    public async Task Body_AndItsContentType_AreForwardedUnchanged()
    {
        using var content = new StringContent(OrderJson, Encoding.UTF8, "application/json");

        await Client.PostAsync("/api/orders", content, TestContext.Current.CancellationToken);

        var received = OrderService.LastRequest;
        Assert.NotNull(received);
        Assert.Equal(OrderJson, received.Body);
        Assert.Equal("application/json; charset=utf-8", received.Headers["Content-Type"]);
    }

    [Fact]
    public async Task ChunkedBody_WithoutContentLength_IsForwarded()
    {
        // A client that streams its body doesn't know the length up front. The gateway must
        // forward every body, not only bodies that announce a Content-Length.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = new StringContent(OrderJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.TransferEncodingChunked = true;

        await Client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(OrderJson, OrderService.LastRequest?.Body);
    }

    [Fact]
    public async Task EndToEndHeaders_AreForwarded()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/42");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "token-123");
        request.Headers.Add("X-Request-Id", "req-1");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");   // passed through, never added (decision 4)

        await Client.SendAsync(request, TestContext.Current.CancellationToken);

        var headers = UserService.LastRequest?.Headers;
        Assert.NotNull(headers);
        Assert.Equal("application/json", headers["Accept"]);
        Assert.Equal("Bearer token-123", headers["Authorization"]);
        Assert.Equal("req-1", headers["X-Request-Id"]);
        Assert.Equal("203.0.113.7", headers["X-Forwarded-For"]);
    }

    [Fact]
    public async Task HopByHopHeaders_AreNotForwarded()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users/42");
        request.Headers.Connection.Add("X-Hop");   // declares X-Hop hop-by-hop, for this connection only
        request.Headers.Add("X-Hop", "for the gateway only");
        request.Headers.TryAddWithoutValidation("Keep-Alive", "timeout=5");
        request.Headers.TryAddWithoutValidation("Proxy-Authorization", "Basic Z2F0ZXdheTpzZWNyZXQ=");
        request.Headers.Add("X-End-To-End", "kept");

        await Client.SendAsync(request, TestContext.Current.CancellationToken);

        var headers = UserService.LastRequest?.Headers;
        Assert.NotNull(headers);
        Assert.False(headers.ContainsKey("X-Hop"));
        Assert.False(headers.ContainsKey("Keep-Alive"));
        Assert.False(headers.ContainsKey("Proxy-Authorization"));
        Assert.DoesNotContain("X-Hop", headers.GetValueOrDefault("Connection") ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal("kept", headers["X-End-To-End"]);
    }

    [Fact]
    public async Task Host_IsTheUpstreams_NotTheGateways()
    {
        await Client.GetAsync("/api/users/42", TestContext.Current.CancellationToken);

        Assert.Equal(new Uri(UserService.BaseUrl).Authority, UserService.LastRequest?.Headers["Host"]);
    }
}
