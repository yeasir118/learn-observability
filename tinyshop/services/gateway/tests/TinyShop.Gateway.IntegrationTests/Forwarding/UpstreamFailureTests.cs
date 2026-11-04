using System.Net;
using Microsoft.AspNetCore.Http;
using TinyShop.Gateway.IntegrationTests.Infrastructure;
using TinyShop.Testing;
using static TinyShop.Testing.ResponseAssertions;

namespace TinyShop.Gateway.IntegrationTests.Forwarding;

/// <summary>
/// When the gateway gets no answer, the error is its own (contracts.md §2.3, TS-014 decision 6).
/// When the upstream does answer, even with an error, the answer is the upstream's (§2.4).
/// </summary>
public sealed class UpstreamFailureTests
{
    [Fact]
    public async Task StoppedUpstream_Returns502UpstreamUnreachable()
    {
        await using var gateway = new GatewayFactory { ProductServiceUrl = $"http://127.0.0.1:{ClosedPort.Get()}/" };
        using var client = gateway.CreateClient();

        var response = await client.GetAsync("/api/products", TestContext.Current.CancellationToken);

        var body = await AssertProblemAsync(response, HttpStatusCode.BadGateway, "upstream_unreachable", "/api/products");
        // The exception's message names the internal address; it must not reach the client.
        Assert.DoesNotContain("127.0.0.1", body.ToJsonString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownUpstreamHost_Returns502UpstreamUnreachable()
    {
        // .invalid is reserved (RFC 2606): it never resolves, anywhere.
        await using var gateway = new GatewayFactory { UserServiceUrl = "http://user-service.invalid/" };
        using var client = gateway.CreateClient();

        var response = await client.GetAsync("/api/users/42", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadGateway, "upstream_unreachable", "/api/users/42");
    }

    [Fact]
    public async Task UpstreamThatDoesNotAnswerInTime_Returns504UpstreamTimeout()
    {
        await using var orderService = await FakeUpstream.StartAsync();
        orderService.Handler = context => Task.Delay(Timeout.Infinite, context.RequestAborted);
        await using var gateway = new GatewayFactory
        {
            OrderServiceUrl = orderService.BaseUrl,
            UpstreamTimeout = TimeSpan.FromSeconds(1),
        };
        using var client = gateway.CreateClient();

        var response = await client.GetAsync("/api/orders/1001", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.GatewayTimeout, "upstream_timeout", "/api/orders/1001");
    }

    [Fact]
    public async Task ClientThatDisconnects_GetsNo504_AndTheUpstreamCallIsCancelled()
    {
        // Both a timeout and a disconnect cancel the upstream call. Only the timeout is a 504: after
        // a disconnect nobody is waiting, and the upstream should stop working for nobody too.
        var upstreamReached = new TaskCompletionSource();
        var upstreamCancelled = new TaskCompletionSource();
        await using var orderService = await FakeUpstream.StartAsync();
        orderService.Handler = async context =>
        {
            context.RequestAborted.Register(() => upstreamCancelled.TrySetResult());
            upstreamReached.SetResult();
            await Task.Delay(Timeout.Infinite, context.RequestAborted);
        };
        await using var gateway = new GatewayFactory
        {
            OrderServiceUrl = orderService.BaseUrl,
            UpstreamTimeout = TimeSpan.FromSeconds(30),   // long: the client leaves first
        };
        using var client = gateway.CreateClient();
        using var leave = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var request = client.GetAsync("/api/orders/1001", leave.Token);
        await upstreamReached.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await leave.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);

        await upstreamCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var status = await WaitForCompletedStatusAsync(gateway);
        Assert.NotEqual(StatusCodes.Status504GatewayTimeout, status);
    }

    [Fact]
    public async Task UpstreamThatFailsMidBody_BreaksTheResponse_InsteadOfEndingItEarly()
    {
        // The status (200) is already sent when the upstream fails, so no error response is
        // possible anymore. The client must notice: a truncated body that looks complete would be
        // silently wrong data. Chunked (no Content-Length), the hard case: with a Content-Length,
        // Kestrel itself would notice the missing bytes; chunked, only the forwarder can.
        // The upstream fails only once the client has the 200: aborting right after the first write
        // can make the reset overtake the headers, and that's the 502 case, not this one.
        var clientHasHeaders = new TaskCompletionSource();
        await using var productService = await FakeUpstream.StartAsync();
        productService.Handler = async context =>
        {
            await context.Response.WriteAsync("""{"items":[""");
            await context.Response.Body.FlushAsync();
            await clientHasHeaders.Task.WaitAsync(TimeSpan.FromSeconds(5));
            context.Abort();
        };
        await using var gateway = new GatewayFactory { ProductServiceUrl = productService.BaseUrl };
        using var client = gateway.CreateClient();

        using var response = await client.GetAsync(
            "/api/products", HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        clientHasHeaders.SetResult();

        // The gateway aborts its connection to the client, so reading the rest of the body fails.
        await Assert.ThrowsAsync<HttpRequestException>(
            () => response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(StatusCodes.Status404NotFound, false)]
    [InlineData(StatusCodes.Status500InternalServerError, false)]
    [InlineData(StatusCodes.Status502BadGateway, false)]   // the upstream's own 502: not upstream_unreachable
    [InlineData(StatusCodes.Status504GatewayTimeout, false)]
    [InlineData(StatusCodes.Status404NotFound, true)]
    [InlineData(StatusCodes.Status502BadGateway, true)]
    public async Task UpstreamErrorWithoutABody_PassesThroughWithoutOne(int status, bool chunked)
    {
        // The gateway's status-code pages would give an empty error a Problem Details body. A
        // forwarded response must arrive as the upstream sent it (TS-014, decision 5).
        // Chunked is the case that needs the forwarder to switch them off: an empty response
        // normally carries "Content-Length: 0", which status-code pages already leave alone.
        await using var userService = await FakeUpstream.StartAsync();
        userService.Handler = async context =>
        {
            context.Response.StatusCode = status;
            if (chunked)
            {
                // Starting the body without writing to it: headers go out chunked, then the
                // empty final chunk.
                await context.Response.StartAsync();
            }
        };
        await using var gateway = new GatewayFactory { UserServiceUrl = userService.BaseUrl };
        using var client = gateway.CreateClient();

        var response = await client.GetAsync("/api/users/42", TestContext.Current.CancellationToken);

        Assert.Equal(status, (int)response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpstreamProblemDetails_PassesThroughWithItsOwnCodeAndTraceId()
    {
        // contracts.md §2.2: order-service's 422 when the user doesn't exist. Its traceId is the
        // one to search the logs for, so the gateway must not replace the body.
        const string problem = """{"type":"https://tinyshop.example/problems/user_not_found","title":"User not found","status":422,"instance":"/orders","code":"user_not_found","traceId":"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"}""";
        await using var orderService = await FakeUpstream.StartAsync();
        orderService.Handler = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(problem);
        };
        await using var gateway = new GatewayFactory { OrderServiceUrl = orderService.BaseUrl };
        using var client = gateway.CreateClient();
        using var content = new StringContent("""{"userId":999,"items":[]}""", System.Text.Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/orders", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(problem, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>The gateway finishes the request on its own schedule after the client left: wait for it.</summary>
    private static async Task<int> WaitForCompletedStatusAsync(GatewayFactory gateway)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (gateway.CompletedStatuses.TryPeek(out var status))
            {
                return status;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("The gateway never finished the request.");
    }
}
