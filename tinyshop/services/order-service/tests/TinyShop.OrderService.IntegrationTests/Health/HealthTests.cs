using System.Net;
using TinyShop.OrderService.IntegrationTests.Infrastructure;

namespace TinyShop.OrderService.IntegrationTests.Health;

/// <summary>Health endpoints while the database is up (contracts.md §5.4).</summary>
public class HealthTests(PostgresFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_WhenDatabaseIsUp_ReturnsHealthy(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
