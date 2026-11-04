using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TinyShop.Testing;

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
    /// <summary>A port nothing listens on: the default for every upstream a test doesn't set.</summary>
    public static readonly string Nowhere = $"http://127.0.0.1:{ClosedPort.Get()}/";

    public GatewayFactory() => UseKestrel(0);   // port 0: the OS picks a free one

    /// <summary>
    /// Where the gateway finds product-service. The default is <see cref="Nowhere"/>, for tests
    /// that never reach an upstream. (A property, not a constructor argument: xUnit creates class
    /// fixtures with a parameterless constructor.)
    /// </summary>
    public string ProductServiceUrl { get; init; } = Nowhere;

    /// <summary>Where the gateway finds user-service. Same default as <see cref="ProductServiceUrl"/>.</summary>
    public string UserServiceUrl { get; init; } = Nowhere;

    /// <summary>Where the gateway finds order-service. Same default as <see cref="ProductServiceUrl"/>.</summary>
    public string OrderServiceUrl { get; init; } = Nowhere;

    /// <summary>The gateway's HttpClient.Timeout for every upstream. Null: the default 100 s.</summary>
    public TimeSpan? UpstreamTimeout { get; init; }

    /// <summary>
    /// The status of every response the gateway finished, as the gateway itself saw it, including
    /// requests whose client had already gone (nothing reached the client, but the gateway still
    /// decided a status).
    /// </summary>
    public ConcurrentQueue<int> CompletedStatuses { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that would enable Swagger. Tests must not depend on whoever runs them.
        builder.UseEnvironment("Testing");

        // UseSetting, not ConfigureAppConfiguration: the gateway reads these while Program.cs runs,
        // before ConfigureAppConfiguration callbacks would.
        builder.UseSetting("Services:ProductService:BaseUrl", ProductServiceUrl);
        builder.UseSetting("Services:UserService:BaseUrl", UserServiceUrl);
        builder.UseSetting("Services:OrderService:BaseUrl", OrderServiceUrl);
        if (UpstreamTimeout is { } timeout)
        {
            builder.UseSetting("Forwarding:UpstreamTimeout", timeout.ToString("c"));
        }

        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new RecordStatuses(CompletedStatuses)));
    }

    /// <summary>A broken response must fail the test in seconds, not after HttpClient's default 100 s.</summary>
    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.Timeout = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Adds a middleware in front of the gateway's whole pipeline that records each response's
    /// final status. A startup filter runs before Program.cs's middleware, so it sees what the
    /// exception handler made of a failure.
    /// </summary>
    private sealed class RecordStatuses(ConcurrentQueue<int> statuses) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                try
                {
                    await nextMiddleware(context);
                }
                finally
                {
                    statuses.Enqueue(context.Response.StatusCode);
                }
            });
            next(app);
        };
    }
}
