using TinyShop.OrderService.Api.Errors;
using TinyShop.ServiceDefaults;

namespace TinyShop.OrderService.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    /// <summary>Registers everything the HTTP layer needs.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        // Controllers, Problem Details, versioning, Swagger, health checks: the same in every service.
        services.AddServiceDefaults(new ServiceDefaultsOptions
        {
            ApiTitle = "TinyShop Order Service",
            ApiDescription = "Internal API for placing and looking up orders. See tinyshop/docs/contracts.md §5.3 and §6.",
            AddErrorCodes = OrderErrorCodes.Register,
        });

        services.AddExceptionHandler<ApplicationExceptionHandler>();

        return services;
    }
}
