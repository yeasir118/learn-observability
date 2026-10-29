using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TinyShop.ServiceDefaults.Abstractions;

namespace TinyShop.ServiceDefaults.UnitTests;

/// <summary>
/// AddServiceDefaults and UseServiceDefaults on their own, in an app with no controllers and no
/// health checks of its own: the shared plumbing must work without any service's code
/// (the gateway, TS-014, will be such an app). In memory: no port is opened.
/// </summary>
public sealed class ServiceDefaultsPipelineTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddServiceDefaults(new ServiceDefaultsOptions
        {
            ApiTitle = "Example",
            ApiDescription = "Example API.",
        });

        _app = builder.Build();
        _app.UseServiceDefaults();
        // Stands in for a repository call whose database is down.
        _app.MapGet("/database-down", () =>
            Task.FromException(new DataStoreUnavailableException("The database is unavailable.", new TimeoutException())));
        await _app.StartAsync(TestContext.Current.CancellationToken);
        _client = _app.GetTestClient();
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_WorkWithoutAnyChecks(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnknownRoute_GetsAProblemDetailsBody()
    {
        var response = await _client.GetAsync("/no-such-route", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("not_found", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("/no-such-route", body.RootElement.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task DataStoreUnavailable_Becomes503ServiceUnavailable()
    {
        var response = await _client.GetAsync("/database-down", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("service_unavailable", body.RootElement.GetProperty("code").GetString());
        // The exception's message and the inner exception never reach the client.
        Assert.Equal("The service is temporarily unavailable. Try again later.", body.RootElement.GetProperty("detail").GetString());
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
    }
}
