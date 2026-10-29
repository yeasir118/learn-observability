using System.Net;
using TinyShop.OrderService.IntegrationTests.Infrastructure;
using TinyShop.OrderService.IntegrationTests.Orders;
using TinyShop.Testing;
using static TinyShop.Testing.ResponseAssertions;

namespace TinyShop.OrderService.IntegrationTests.Database;

/// <summary>
/// A second copy of the app whose connection string points at a port nothing listens on.
/// That reproduces "database unreachable" without stopping the container other tests share.
/// </summary>
public sealed class UnreachableDatabaseFixture : IDisposable
{
    public UnreachableDatabaseFixture()
    {
        var connectionString = UnreachableDatabase.ConnectionString("tinyshop_orders");
        Factory = new OrderServiceFactory(connectionString);
    }

    public OrderServiceFactory Factory { get; }

    public void Dispose() => Factory.Dispose();
}

/// <summary>contracts.md §2.2, §5.4 and §6: an unreachable order-db is 503, and liveness stays up.</summary>
public class DatabaseUnavailableTests(UnreachableDatabaseFixture fixture) : IClassFixture<UnreachableDatabaseFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Theory]
    [InlineData("/orders/1001", "/orders/1001")]
    [InlineData("/orders?userId=123", "/orders")]
    public async Task ReadEndpoint_WhenDatabaseIsUnreachable_Returns503ServiceUnavailable(string path, string instance)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "service_unavailable", instance);
    }

    [Fact]
    public async Task PlaceOrder_WhenTheInsertFails_Returns503WithoutOrderId_AndNothingIsCharged()
    {
        // §6 "order-db: insert fails (before payment)": user and product were checked, then the
        // insert failed, so there's no order and payment-service is never called.
        var response = await _client.PostOrderAsync(Scenarios.NewUserId(), 1, 1);

        var problem = await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "service_unavailable", "/orders");
        Assert.Null(problem["orderId"]);
        Assert.Empty(fixture.Factory.Dependencies.PaymentRequests);
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
