using TinyShop.Gateway.Api.Errors;
using TinyShop.Gateway.Api.Forwarding;
using TinyShop.ServiceDefaults;

namespace TinyShop.Gateway.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    private const string ProductServiceUrlKey = "Services:ProductService:BaseUrl";
    private const string UserServiceUrlKey = "Services:UserService:BaseUrl";
    private const string OrderServiceUrlKey = "Services:OrderService:BaseUrl";
    private const string UpstreamTimeoutKey = "Forwarding:UpstreamTimeout";

    /// <summary>Registers everything the gateway needs.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        // Problem Details (with the gateway's own error codes), health checks, Swagger: the same in
        // every service. The gateway has no controllers; the shared setup works without them.
        services.AddServiceDefaults(new ServiceDefaultsOptions
        {
            ApiTitle = "TinyShop Gateway",
            ApiDescription = "The public API (/api/*), forwarded to the internal services. See tinyshop/docs/contracts.md §4.",
            AddErrorCodes = GatewayErrorCodes.Register,
        });

        // 502 and 504 when an upstream can't be reached or doesn't answer in time. Runs alongside
        // the shared handlers: each one handles only its own exceptions.
        services.AddExceptionHandler<UpstreamExceptionHandler>();

        // Singleton: it holds no per-request state, and IHttpClientFactory is a singleton too.
        services.AddSingleton<RequestForwarder>();

        // Read everything before registering anything: a missing or malformed setting stops the
        // gateway at startup, instead of on the first request.
        var productServiceUrl = GetRequiredUrl(configuration, ProductServiceUrlKey);
        var userServiceUrl = GetRequiredUrl(configuration, UserServiceUrlKey);
        var orderServiceUrl = GetRequiredUrl(configuration, OrderServiceUrlKey);
        var timeout = GetOptionalTimeout(configuration, UpstreamTimeoutKey);

        // Named clients: the gateway treats every upstream the same way, so it needs a
        // per-upstream base address, not per-upstream methods. IHttpClientFactory pools the
        // connections underneath. The names are the ones the routes use (GatewayRoutes), so the
        // two can't drift apart.
        services.AddUpstream(GatewayRoutes.ProductService, productServiceUrl, timeout);
        services.AddUpstream(GatewayRoutes.UserService, userServiceUrl, timeout);
        services.AddUpstream(GatewayRoutes.OrderService, orderServiceUrl, timeout);

        return services;
    }

    private static void AddUpstream(this IServiceCollection services, string name, Uri baseAddress, TimeSpan? timeout) =>
        services.AddHttpClient(name, client =>
        {
            client.BaseAddress = baseAddress;

            // Unset: HttpClient's default 100 s (D5, decision 7). The setting exists so tests can
            // make "the upstream doesn't answer" take a second instead of 100.
            if (timeout is { } value)
            {
                client.Timeout = value;
            }
        });

    private static Uri GetRequiredUrl(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (!Uri.TryCreate(value, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                $"'{key}' must be set to an absolute http(s) URL, such as http://product-service:8080.");
        }

        // Relative request paths ("products") are appended to the base address only when it ends
        // with a slash; without one, the last path segment would be replaced.
        return url.AbsoluteUri.EndsWith('/') ? url : new Uri(url.AbsoluteUri + "/");
    }

    private static TimeSpan? GetOptionalTimeout(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!TimeSpan.TryParse(value, out var timeout) || timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                $"'{key}' must be a positive duration, such as 00:00:30, or not set at all (100 s).");
        }

        return timeout;
    }
}
