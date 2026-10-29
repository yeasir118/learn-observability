using TinyShop.ProductService.Api.Errors;
using TinyShop.ServiceDefaults;

namespace TinyShop.ProductService.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    /// <summary>Registers everything the HTTP layer needs.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        // Controllers, Problem Details, versioning, Swagger, health checks: the same in every service.
        services.AddServiceDefaults(new ServiceDefaultsOptions
        {
            ApiTitle = "TinyShop Product Service",
            ApiDescription = "Internal API for the product catalogue. See tinyshop/docs/contracts.md §5.1.",
            AddErrorCodes = ProductErrorCodes.Register,
        });

        services.AddExceptionHandler<DataStoreUnavailableExceptionHandler>();

        return services;
    }
}
