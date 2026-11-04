using System.Net;
using TinyShop.Gateway.IntegrationTests.Infrastructure;
using static TinyShop.Testing.ResponseAssertions;

namespace TinyShop.Gateway.IntegrationTests.Routing;

/// <summary>
/// Only the five public routes exist (contracts.md §4). Everything else is the gateway's own
/// 404 not_found: it never reaches an internal service.
/// </summary>
public class UnknownRouteTests(GatewayFactory factory) : IClassFixture<GatewayFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/api/no-such-route")]
    [InlineData("/products")]          // an internal path, not a public one
    [InlineData("/api/products/1")]    // exists internally, not publicly
    public async Task UnknownPath_Returns404NotFound(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found", path);
    }
}
