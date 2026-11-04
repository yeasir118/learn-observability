using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using TinyShop.Gateway.IntegrationTests.Infrastructure;

namespace TinyShop.Gateway.IntegrationTests.Forwarding;

/// <summary>
/// Phase 2: GET /api/products, forwarded to product-service's GET /products (contracts.md §4), against
/// a real upstream server. The gateway must pass the answer through unchanged (§2.4) and stream it.
/// </summary>
public sealed class GetProductsTests : IAsyncLifetime
{
    private FakeUpstream _productService = null!;
    private GatewayFactory _gateway = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _productService = await FakeUpstream.StartAsync();
        _gateway = new GatewayFactory { ProductServiceUrl = _productService.BaseUrl };
        _client = _gateway.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _gateway.DisposeAsync();
        await _productService.DisposeAsync();
    }

    [Fact]
    public async Task GetProducts_IsForwardedToTheInternalPath()
    {
        await _client.GetAsync("/api/products", TestContext.Current.CancellationToken);

        Assert.NotNull(_productService.LastRequest);
        Assert.Equal("GET", _productService.LastRequest.Method);
        Assert.Equal("/products", _productService.LastRequest.Path);
    }

    [Fact]
    public async Task GetProducts_PassesStatusHeadersAndBodyThroughUnchanged()
    {
        const string body = """{"items":[{"id":1,"name":"Keyboard","priceCents":10000}]}""";
        _productService.Handler = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers["X-Upstream-Header"] = "from product-service";
            await context.Response.WriteAsync(body);
        };

        var response = await _client.GetAsync("/api/products", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("from product-service", Assert.Single(response.Headers.GetValues("X-Upstream-Header")));
        Assert.Equal(body, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetProducts_PassesAnUpstreamErrorThroughUnchanged()
    {
        // The service's own error, not the gateway's: it must arrive exactly as sent (§2.4 rule 1).
        const string problem = """{"type":"https://tinyshop.example/problems/service_unavailable","title":"Service unavailable","status":503,"code":"service_unavailable","traceId":"from-product-service"}""";
        _productService.Handler = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(problem);
        };

        var response = await _client.GetAsync("/api/products", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(problem, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetProducts_StreamsTheBody_FirstBytesArriveBeforeTheUpstreamHasFinished()
    {
        // The upstream sends its first line, then waits until the test has received it. A gateway
        // that buffers the whole body would wait for the end, and the test would time out.
        var firstLineReceived = new TaskCompletionSource();
        _productService.Handler = async context =>
        {
            await context.Response.WriteAsync("first line\n");
            await context.Response.Body.FlushAsync();
            await firstLineReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await context.Response.WriteAsync("second line\n");
        };

        using var response = await _client.GetAsync(
            "/api/products", HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), Encoding.UTF8);

        var firstLine = await reader.ReadLineAsync(TestContext.Current.CancellationToken)
            .AsTask().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        firstLineReceived.SetResult();

        Assert.Equal("first line", firstLine);
        Assert.Equal("second line", await reader.ReadLineAsync(TestContext.Current.CancellationToken));
    }
}
