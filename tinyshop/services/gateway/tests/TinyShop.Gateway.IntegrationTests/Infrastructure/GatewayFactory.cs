using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TinyShop.Gateway.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the gateway in memory: the real Program.cs and pipeline, with an HTTP client that calls
/// it without opening a network port.
/// </summary>
public sealed class GatewayFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        // Not "Development": that would enable Swagger. Tests must not depend on whoever runs them.
        builder.UseEnvironment("Testing");
}
