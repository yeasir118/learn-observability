using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TinyShop.Gateway.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts the gateway for tests: the real Program.cs and pipeline, on real Kestrel at a random
/// port on 127.0.0.1, with an HTTP client pointed at it.
/// </summary>
/// <remarks>
/// Real Kestrel, not the default in-memory test server: in memory, no HTTP is written between the
/// test client and the gateway, so framing bugs (copying Transfer-Encoding) and hop-by-hop headers
/// would go unnoticed (TS-014, decision 10).
/// </remarks>
public sealed class GatewayFactory : WebApplicationFactory<Program>
{
    public GatewayFactory() => UseKestrel(0);   // port 0: the OS picks a free one

    /// <summary>
    /// Where the gateway finds product-service. The default is the discard port, where nothing
    /// listens, for tests that never reach an upstream. (A property, not a constructor argument:
    /// xUnit creates class fixtures with a parameterless constructor.)
    /// </summary>
    public string ProductServiceUrl { get; init; } = "http://127.0.0.1:9/";

    /// <summary>Where the gateway finds user-service. Same default as <see cref="ProductServiceUrl"/>.</summary>
    public string UserServiceUrl { get; init; } = "http://127.0.0.1:9/";

    /// <summary>Where the gateway finds order-service. Same default as <see cref="ProductServiceUrl"/>.</summary>
    public string OrderServiceUrl { get; init; } = "http://127.0.0.1:9/";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that would enable Swagger. Tests must not depend on whoever runs them.
        builder.UseEnvironment("Testing");

        // UseSetting, not ConfigureAppConfiguration: the gateway reads these while Program.cs runs,
        // before ConfigureAppConfiguration callbacks would.
        builder.UseSetting("Services:ProductService:BaseUrl", ProductServiceUrl);
        builder.UseSetting("Services:UserService:BaseUrl", UserServiceUrl);
        builder.UseSetting("Services:OrderService:BaseUrl", OrderServiceUrl);
    }

    /// <summary>A broken response must fail the test in seconds, not after HttpClient's default 100 s.</summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.Timeout = TimeSpan.FromSeconds(10);
    }
}
