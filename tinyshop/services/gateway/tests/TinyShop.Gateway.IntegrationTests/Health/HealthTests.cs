using System.Net;
using TinyShop.Gateway.IntegrationTests.Infrastructure;

namespace TinyShop.Gateway.IntegrationTests.Health;

/// <summary>
/// contracts.md §5.4. The gateway checks nothing upstream (TS-014, decision 8), so both are healthy
/// even with no internal service running, as here.
/// </summary>
public class HealthTests(GatewayFactory factory) : IClassFixture<GatewayFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoint_WithNoUpstreamRunning_ReturnsHealthy(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
