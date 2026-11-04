using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using TinyShop.Gateway.IntegrationTests.Infrastructure;

namespace TinyShop.Gateway.IntegrationTests.Forwarding;

/// <summary>
/// What the client receives: the upstream's answer unchanged, except the hop-by-hop headers of the
/// upstream's connection, and Location rewritten to the public path (contracts.md §2.4 rule 3).
/// </summary>
public sealed class ResponseForwardingTests : GatewayWithUpstreams
{
    [Fact]
    public async Task Location_IsRewrittenToThePublicPath()
    {
        const string order = """{"id":1001,"status":"confirmed"}""";
        OrderService.Handler = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status201Created;
            context.Response.Headers.Location = "/orders/1001";
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsync(order);
        };
        using var content = new StringContent("""{"userId":42,"items":[{"productId":1,"quantity":1}]}""", Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/orders", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/orders/1001", response.Headers.Location?.OriginalString);
        Assert.Equal(order, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HopByHopHeaders_OfTheUpstreamConnection_AreNotPassedBack()
    {
        UserService.Handler = async context =>
        {
            context.Response.Headers.Connection = "X-Hop";
            context.Response.Headers["X-Hop"] = "for the gateway only";
            context.Response.Headers["Keep-Alive"] = "timeout=5";
            context.Response.Headers["X-End-To-End"] = "kept";
            await context.Response.WriteAsJsonAsync(new { id = 42 });
        };

        var response = await Client.GetAsync("/api/users/42", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("X-Hop"));
        Assert.False(response.Headers.Contains("Keep-Alive"));
        Assert.DoesNotContain("X-Hop", response.Headers.Connection);
        Assert.Equal("kept", Assert.Single(response.Headers.GetValues("X-End-To-End")));
    }
}
