using System.Net;
using System.Net.Sockets;
using TinyShop.UserService.IntegrationTests.Infrastructure;
using static TinyShop.UserService.IntegrationTests.Support.ResponseAssertions;

namespace TinyShop.UserService.IntegrationTests.Database;

/// <summary>
/// A second copy of the app whose connection string points at a port nothing listens on.
/// That reproduces "database unreachable" without stopping the container other tests share.
/// </summary>
public sealed class UnreachableDatabaseFixture : IDisposable
{
    public UnreachableDatabaseFixture()
    {
        // 127.0.0.1, not localhost: on Windows, localhost tries IPv6 first and each refused
        // connection attempt costs about two seconds.
        var connectionString =
            $"Host=127.0.0.1;Port={GetClosedPort()};Database=tinyshop_users;Username=x;Password=x";
        Factory = new UserServiceFactory(connectionString);
    }

    public UserServiceFactory Factory { get; }

    public void Dispose() => Factory.Dispose();

    /// <summary>
    /// Asks the OS for a free port, then releases it, so nothing is listening there.
    /// </summary>
    private static int GetClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

/// <summary>contracts.md §2.2 and §5.4: an unreachable database is 503, and liveness stays up.</summary>
public class DatabaseUnavailableTests(UnreachableDatabaseFixture fixture) : IClassFixture<UnreachableDatabaseFixture>
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetUser_WhenDatabaseIsUnreachable_Returns503ServiceUnavailable()
    {
        var response = await _client.GetAsync("/users/123", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "service_unavailable", "/users/123");
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
