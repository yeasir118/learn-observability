using System.Net;
using TinyShop.ProductService.IntegrationTests.Infrastructure;
using TinyShop.Testing;
using static TinyShop.Testing.ResponseAssertions;

namespace TinyShop.ProductService.IntegrationTests.Database;

/// <summary>
/// A second copy of the app whose connection string points at a port nothing listens on.
/// That reproduces "database unreachable" without stopping the container other tests share.
/// </summary>
public sealed class UnreachableDatabaseFixture : IDisposable
{
    public UnreachableDatabaseFixture()
    {
        var connectionString = UnreachableDatabase.ConnectionString("tinyshop_products");
        Factory = new ProductServiceFactory(connectionString);
    }

    public ProductServiceFactory Factory { get; }

    public void Dispose() => Factory.Dispose();
}

/// <summary>contracts.md §2.2 and §5.4: an unreachable database is 503, and liveness stays up.</summary>
public class DatabaseUnavailableTests(UnreachableDatabaseFixture fixture) : IClassFixture<UnreachableDatabaseFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Theory]
    [InlineData("/products")]
    [InlineData("/products/1")]
    public async Task ProductEndpoint_WhenDatabaseIsUnreachable_Returns503ServiceUnavailable(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "service_unavailable", path);
    }

    [Fact]
    public async Task Liveness_WhenDatabaseIsUnreachable_StillReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Readiness_WhenDatabaseIsUnreachable_Returns503Unhealthy()
    {
        var response = await _client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
