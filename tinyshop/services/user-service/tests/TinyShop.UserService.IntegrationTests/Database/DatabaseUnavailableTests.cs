using System.Net;
using TinyShop.Testing;
using TinyShop.UserService.IntegrationTests.Infrastructure;
using static TinyShop.Testing.ResponseAssertions;

namespace TinyShop.UserService.IntegrationTests.Database;

/// <summary>
/// A second copy of the app whose connection string points at a port nothing listens on.
/// That reproduces "database unreachable" without stopping the container other tests share.
/// </summary>
public sealed class UnreachableDatabaseFixture : IDisposable
{
    public UnreachableDatabaseFixture()
    {
        var connectionString = UnreachableDatabase.ConnectionString("tinyshop_users");
        Factory = new UserServiceFactory(connectionString);
    }

    public UserServiceFactory Factory { get; }

    public void Dispose() => Factory.Dispose();
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
