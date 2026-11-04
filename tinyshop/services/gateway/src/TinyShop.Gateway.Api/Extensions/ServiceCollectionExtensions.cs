using TinyShop.Gateway.Api.Errors;
using TinyShop.ServiceDefaults;

namespace TinyShop.Gateway.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    /// <summary>Registers everything the gateway needs.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        // Problem Details (with the gateway's own error codes), health checks, Swagger: the same in
        // every service. The gateway has no controllers; the shared setup works without them.
        services.AddServiceDefaults(new ServiceDefaultsOptions
        {
            ApiTitle = "TinyShop Gateway",
            ApiDescription = "The public API (/api/*), forwarded to the internal services. See tinyshop/docs/contracts.md §4.",
            AddErrorCodes = GatewayErrorCodes.Register,
        });

        return services;
    }
}
