using System.Net;
using TinyShop.UserService.IntegrationTests.Infrastructure;
using static TinyShop.UserService.IntegrationTests.Support.ResponseAssertions;

namespace TinyShop.UserService.IntegrationTests.Routing;

/// <summary>
/// Errors produced by the framework rather than by our controllers (unknown routes, wrong methods,
/// API versions) must follow the same Problem Details contract (contracts.md §1, §2.3).
/// </summary>
public class RoutingAndVersioningTests(PostgresFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task UnknownRoute_Returns404NotFound()
    {
        var response = await _client.GetAsync("/no-such-route", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "not_found", "/no-such-route");
    }

    [Fact]
    public async Task UnsupportedMethod_Returns405WithAllowHeader()
    {
        var response = await _client.DeleteAsync("/users/123", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.MethodNotAllowed, "method_not_allowed", "/users/123");
        Assert.Contains("GET", response.Content.Headers.Allow);
    }

    [Fact]
    public async Task NoApiVersion_DefaultsTo1_0AndReportsSupportedVersions()
    {
        var response = await _client.GetAsync("/users/123", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1.0", Assert.Single(response.Headers.GetValues("api-supported-versions")));
    }

    [Fact]
    public async Task ApiVersion1_0AsQueryString_IsAccepted()
    {
        var response = await _client.GetAsync("/users/123?api-version=1.0", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiVersion1_0AsHeader_IsAccepted()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/users/123");
        request.Headers.Add("api-version", "1.0");

        var response = await _client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("banana")]
    public async Task UnsupportedApiVersion_Returns400UnsupportedApiVersion(string version)
    {
        var response = await _client.GetAsync($"/users/123?api-version={version}", TestContext.Current.CancellationToken);

        // AssertProblemAsync also checks the detail doesn't leak the host name, which the
        // versioning library's own message would.
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "unsupported_api_version", "/users/123");
    }
}
