using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using V1 = TinyShop.Payment.V1;

namespace TinyShop.OrderService.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts order-service in memory: the real Program.cs, middleware, controllers, EF Core, typed
/// HttpClients and gRPC client, with an HTTP client that calls it without opening a network port.
/// Only the network under the dependency clients is fake (<see cref="FakeDependencies"/>).
/// </summary>
public sealed class OrderServiceFactory(string connectionString, Func<Task>? duringPayment = null)
    : WebApplicationFactory<Program>
{
    /// <summary>How long the fake "hung" user- or product-service is waited for (production: 100 s).</summary>
    public static readonly TimeSpan DependencyTimeout = TimeSpan.FromSeconds(1);

    public FakeDependencies Dependencies { get; } = new(duringPayment);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not "Development": that would load the developer's user secrets (which point at the
        // real database) and enable Swagger. Tests must not depend on whoever runs them.
        builder.UseEnvironment("Testing");

        // UseSetting, not ConfigureAppConfiguration: AddInfrastructure reads these while Program.cs
        // runs. UseSetting values are already in builder.Configuration at that point;
        // ConfigureAppConfiguration callbacks only run later, inside builder.Build().
        builder.UseSetting("ConnectionStrings:OrderDb", connectionString);
        builder.UseSetting("Services:UserService:BaseUrl", FakeDependencies.UserServiceUrl.ToString());
        builder.UseSetting("Services:ProductService:BaseUrl", FakeDependencies.ProductServiceUrl.ToString());
        builder.UseSetting("Services:PaymentService:BaseUrl", FakeDependencies.PaymentServiceUrl.ToString());

        // Runs after Program.cs registered the real services, so these replace them.
        builder.ConfigureTestServices(services =>
        {
            // Every IHttpClientFactory client (the user- and product-service typed clients) sends
            // through the fake handler instead of a socket.
            services.ConfigureHttpClientDefaults(client => client
                .ConfigurePrimaryHttpMessageHandler(() => Dependencies.HttpHandler)
                .ConfigureHttpClient(httpClient => httpClient.Timeout = DependencyTimeout));

            // The last registration wins: the generated client, over the fake invoker.
            services.AddSingleton(new V1.PaymentService.PaymentServiceClient(Dependencies.PaymentInvoker));
        });
    }
}
