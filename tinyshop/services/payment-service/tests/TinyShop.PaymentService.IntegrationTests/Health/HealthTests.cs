using System.Net;
using Grpc.Health.V1;
using TinyShop.PaymentService.IntegrationTests.Infrastructure;

namespace TinyShop.PaymentService.IntegrationTests.Health;

/// <summary>
/// Both health interfaces: the standard gRPC health protocol (grpc.health.v1), which gRPC-aware
/// probes use, and the /health endpoints every TinyShop service has (contracts.md §5.4).
/// </summary>
public class HealthTests(PaymentServiceFactory factory)
{
    [Fact]
    public async Task GrpcHealthCheck_ReportsServing()
    {
        var client = new global::Grpc.Health.V1.Health.HealthClient(factory.CreateChannel());

        // An empty service name asks about the server as a whole.
        var response = await client.CheckAsync(
            new HealthCheckRequest { Service = string.Empty },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(HealthCheckResponse.Types.ServingStatus.Serving, response.Status);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HttpHealthEndpoint_ReportsHealthy(string path)
    {
        var response = await factory.CreateClient().GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
