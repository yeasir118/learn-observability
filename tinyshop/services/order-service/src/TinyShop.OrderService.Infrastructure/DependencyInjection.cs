using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyShop.OrderService.Application.Abstractions.Dependencies;
using TinyShop.OrderService.Application.Abstractions.Persistence;
using TinyShop.OrderService.Infrastructure.Clients;
using TinyShop.OrderService.Infrastructure.Persistence;
using TinyShop.OrderService.Infrastructure.Persistence.Repositories;
using V1 = TinyShop.Payment.V1;

namespace TinyShop.OrderService.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Health-check tag for checks that decide readiness (see contracts.md §5.4).</summary>
    public const string ReadyTag = "ready";

    /// <summary>Name of the configuration entry under ConnectionStrings.</summary>
    private const string ConnectionStringName = "OrderDb";

    /// <summary>Name of the database health check, as it appears in health reports.</summary>
    private const string DatabaseHealthCheckName = "order-db";

    /// <summary>Configuration keys of the dependencies' base addresses (http://user-service:8080 in Docker).</summary>
    public const string UserServiceUrlKey = "Services:UserService:BaseUrl";
    public const string ProductServiceUrlKey = "Services:ProductService:BaseUrl";
    public const string PaymentServiceUrlKey = "Services:PaymentService:BaseUrl";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Fail at startup, with a clear message, rather than on the first request.
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is missing from configuration "
                + $"(key ConnectionStrings:{ConnectionStringName}).");
        var userServiceUrl = GetRequiredUrl(configuration, UserServiceUrlKey);
        var productServiceUrl = GetRequiredUrl(configuration, ProductServiceUrlKey);
        var paymentServiceUrl = GetRequiredUrl(configuration, PaymentServiceUrlKey);

        services.AddDbContext<OrderDbContext>(options => options.UseOrderDatabase(connectionString));
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Typed clients: IHttpClientFactory creates the HttpClient each one is given and pools the
        // connections underneath (no socket exhaustion, DNS changes picked up). No Timeout is set:
        // the default 100 s applies (contracts.md §7, decision D5).
        services.AddHttpClient<IUserDirectory, UserServiceClient>(client => client.BaseAddress = userServiceUrl);
        services.AddHttpClient<IProductCatalog, ProductServiceClient>(client => client.BaseAddress = productServiceUrl);

        // The gRPC client factory does the same for the generated client. An http:// address means
        // HTTP/2 without TLS (h2c), which is what payment-service listens for.
        services.AddGrpcClient<V1.PaymentService.PaymentServiceClient>(options => options.Address = paymentServiceUrl);
        services.AddScoped<IPaymentGateway, PaymentGateway>();

        // Readiness = "can this service do its job", which means "can it reach its own database".
        // Never user-, product- or payment-service: one failing service would otherwise mark
        // every service that calls it unhealthy too. Liveness checks nothing (contracts.md §5.4).
        services.AddHealthChecks()
            .AddDbContextCheck<OrderDbContext>(name: DatabaseHealthCheckName, tags: [ReadyTag]);

        return services;
    }

    private static Uri GetRequiredUrl(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                $"'{key}' must be set to an absolute http(s) URL, such as http://user-service:8080.");
        }

        // Relative request paths ("users/123") are appended to the base address only when it ends
        // with a slash; without one, the last path segment would be replaced.
        return url.AbsoluteUri.EndsWith('/') ? url : new Uri(url.AbsoluteUri + "/");
    }
}
