using TinyShop.ServiceDefaults;
using TinyShop.UserService.Api.Errors;

namespace TinyShop.UserService.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    /// <summary>Registers everything the HTTP layer needs.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        // Controllers, Problem Details (including "database unavailable" -> 503), versioning,
        // Swagger, health checks: the same in every service.
        services.AddServiceDefaults(new ServiceDefaultsOptions
        {
            ApiTitle = "TinyShop User Service",
            ApiDescription = "Internal API for looking up users. See tinyshop/docs/contracts.md §5.2.",
            AddErrorCodes = UserErrorCodes.Register,
        });

        return services;
    }
}
