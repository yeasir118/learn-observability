using System.Diagnostics;
using System.Net;
using TinyShop.OrderService.IntegrationTests.Infrastructure;
using TinyShop.OrderService.IntegrationTests.Orders;

namespace TinyShop.OrderService.IntegrationTests.Faults;

/// <summary>
/// The database-delay fault on the write path, against a real Postgres (TS-016, AC3, AC5). Placing
/// an order runs every kind of command the fault rewrites: INSERT ... RETURNING (a reader) and the
/// status UPDATE, whose affected-row count EF Core checks. The waiting statement in front of each
/// must change neither the result set nor that count.
/// </summary>
public class DatabaseDelayTests(PostgresFixture fixture)
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(300);

    [Fact]
    public async Task WithADelay_PlacingAnOrderStillWorks_AndEveryCommandWaits()
    {
        await using var factory = new OrderServiceFactory(
            fixture.AppConnectionString,
            settings: new Dictionary<string, string?> { ["Faults:DatabaseDelay"] = "00:00:00.300" });
        var client = factory.CreateClient();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.PostOrderAsync(Scenarios.NewUserId(), Scenarios.Product.Keyboard, 1);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await OrderApi.ReadObjectAsync(response);
        Assert.Equal("PAID", (string?)order["status"]);
        // At least two commands (the insert and the status update), each waiting in Postgres.
        Assert.True(stopwatch.Elapsed >= 2 * Delay, $"Placing an order took only {stopwatch.Elapsed}.");

        // And what was stored is what was returned: the update really happened.
        var stored = await client.GetOrderAsync((long)order["id"]!);
        Assert.Equal("PAID", (string?)stored["status"]);
    }
}
