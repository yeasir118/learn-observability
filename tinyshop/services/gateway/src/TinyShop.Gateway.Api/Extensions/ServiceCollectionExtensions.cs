using TinyShop.Gateway.Api.Errors;
using TinyShop.Gateway.Api.Forwarding;
using TinyShop.ServiceDefaults;

namespace TinyShop.Gateway.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    private const string ProductServiceUrlKey = "Services:ProductService:BaseUrl";

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

        services.AddSingleton<RequestForwarder>();

        // Read before registering anything: a missing or malformed address stops the gateway at
        // startup, instead of on the first request.
        var productServiceUrl = GetRequiredUrl(configuration, ProductServiceUrlKey);

        // A named client: the gateway treats every upstream the same way, so it needs a
        // per-upstream base address, not per-upstream methods. IHttpClientFactory pools the
        // connections underneath. No Timeout is set: the default 100 s applies (decision D5).
        services.AddHttpClient("product-service", client => client.BaseAddress = productServiceUrl);

        return services;
    }

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
}
