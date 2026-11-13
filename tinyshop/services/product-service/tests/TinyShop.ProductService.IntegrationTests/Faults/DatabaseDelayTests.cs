using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Options;
using TinyShop.ProductService.IntegrationTests.Infrastructure;

namespace TinyShop.ProductService.IntegrationTests.Faults;

/// <summary>
/// The database-delay fault against a real Postgres, connected as product_app like in Compose
/// (TS-016, AC3, AC5, AC7): the rewritten command runs, returns the right rows, and takes longer.
/// </summary>
public class DatabaseDelayTests(PostgresFixture fixture)
{
    private static readonly TimeSpan Delay = TimeSpan.FromSeconds(1);

    private ProductServiceFactory WithDelay(string delay) =>
        new(fixture.AppConnectionString, new Dictionary<string, string?> { ["Faults:DatabaseDelay"] = delay });

    [Fact]
    public async Task WithADelay_QueriesReturnTheSameData_AfterWaitingInPostgres()
    {
        await using var factory = WithDelay("00:00:01");
        var client = factory.CreateClient();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/products/2", TestContext.Current.CancellationToken);
        stopwatch.Stop();

        // The waiting statement returns no result set, so EF Core still reads the product.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"Keyboard\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(stopwatch.Elapsed >= Delay, $"The request took only {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task WithADelay_TheReadinessCheckIsNotDelayed()
    {
        // The check opens a connection (CanConnect) but sends no command through EF Core, so the
        // interceptor never sees it. If that ever changes, this test catches it (decision 9).
        // A delay far above any start-up cost: a delayed check would take 20 s, a normal one well
        // under the 10 s limit even while every other test runs in parallel.
        await using var factory = WithDelay("00:00:20");
        var client = factory.CreateClient();
        // Start the host and open the first database connection outside the measurement.
        await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
        await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"The readiness check took {stopwatch.Elapsed}.");
    }

    [Fact]
    public async Task WithoutADelay_AsComposePassesIt_ChangesNothing()
    {
        await using var factory = WithDelay("00:00:00");

        var response = await factory.CreateClient().GetAsync("/products/2", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("00:00:26")]   // over the cap, below Npgsql's 30 s command timeout
    [InlineData("-00:00:01")]
    public async Task InvalidDelay_StopsTheServiceAtStartup_NamingTheSetting(string delay)
    {
        await using var factory = WithDelay(delay);

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("DatabaseDelay", exception.Message);
    }
}
