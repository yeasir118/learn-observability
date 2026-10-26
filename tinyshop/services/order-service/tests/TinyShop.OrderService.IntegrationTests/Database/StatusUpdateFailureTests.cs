using System.Net;
using Npgsql;
using TinyShop.OrderService.IntegrationTests.Infrastructure;
using TinyShop.OrderService.IntegrationTests.Orders;
using static TinyShop.OrderService.IntegrationTests.Database.SqlHelpers;
using static TinyShop.OrderService.IntegrationTests.Support.ResponseAssertions;

namespace TinyShop.OrderService.IntegrationTests.Database;

/// <summary>
/// A copy of the app that reaches Postgres through a <see cref="TcpProxy"/>. The fake
/// payment-service stops the proxy while "processing" the payment, so the order row exists but
/// the database is gone by the time order-service records the result.
/// </summary>
public sealed class DatabaseDownDuringPaymentFixture : IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly TcpProxy _proxy;

    public DatabaseDownDuringPaymentFixture(PostgresFixture postgres)
    {
        _postgres = postgres;
        var (host, port) = postgres.Endpoint;
        _proxy = new TcpProxy(host, port);

        var connectionString = new NpgsqlConnectionStringBuilder(postgres.AppConnectionString)
        {
            Host = "127.0.0.1",
            Port = _proxy.Port,
        }.ConnectionString;
        Factory = new OrderServiceFactory(connectionString, duringPayment: _proxy.StopAsync);
    }

    public OrderServiceFactory Factory { get; }

    public string AdminConnectionString => _postgres.AdminConnectionString;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _proxy.DisposeAsync();
    }
}

/// <summary>
/// §6 "order-db: status update fails (after payment)" and the known gap it leads to (§8): the
/// payment was approved, but the order stays PENDING.
/// </summary>
public class StatusUpdateFailureTests(DatabaseDownDuringPaymentFixture fixture)
    : IClassFixture<DatabaseDownDuringPaymentFixture>
{
    [Fact]
    public async Task DatabaseLostAfterAnApprovedPayment_Returns503WithOrderId_AndTheOrderStaysPending()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PostOrderAsync(Scenarios.Payment.DatabaseDownDuringPayment, 1, 1);

        var problem = await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "service_unavailable", "/orders");
        var orderId = (long)problem["orderId"]!;

        // The payment really was approved...
        Assert.True(fixture.Factory.Dependencies.PaymentRequests.ContainsKey(orderId));
        // ...but the stored order still says PENDING: the customer was charged for an order that
        // looks unpaid. Read directly, since this app's database is now unreachable.
        var status = await ExecuteScalarAsync<string>(
            fixture.AdminConnectionString, $"SELECT status FROM orders WHERE id = {orderId}");
        Assert.Equal("PENDING", status);
    }
}
