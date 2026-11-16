using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using TinyShop.ProductService.Api.Errors;
using TinyShop.ProductService.Api.Faults;
using TinyShop.ServiceDefaults;
using TinyShop.ServiceDefaults.Abstractions.Faults;

namespace TinyShop.ProductService.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    /// <summary>Registers everything the HTTP layer needs.</summary>
    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        // Controllers, Problem Details (including "database unavailable" -> 503), versioning,
        // Swagger, health checks: the same in every service.
        services.AddServiceDefaults(new ServiceDefaultsOptions
        {
            ApiTitle = "TinyShop Product Service",
            ApiDescription = "Internal API for the product catalogue. See tinyshop/docs/contracts.md §5.1.",
            AddErrorCodes = ProductErrorCodes.Register,
        });

        services.AddFaults();

        return services;
    }

    /// <summary>The fault switches (TS-016, docs/faults.md). All off unless configured.</summary>
    private static void AddFaults(this IServiceCollection services)
    {
        services.AddOptions<ProductFaultOptions>()
            .BindConfiguration(ProductFaultOptions.SectionName)
            .ValidateDataAnnotations()
            // A bad value stops the service at startup instead of silently doing nothing (decision 1).
            .ValidateOnStart();
        // Logged at startup when a fault is on (decision 8).
        services.AddSingleton<IFaultSettings>(provider =>
            provider.GetRequiredService<IOptions<ProductFaultOptions>>().Value);

        // The real randomness, unless something (a test) registered another one first.
        services.TryAddSingleton<IFaultRandom, SharedFaultRandom>();

        // Runs for every controller action; it does nothing while ErrorRate is 0.
        services.Configure<MvcOptions>(options => options.Filters.Add<ProductFaultFilter>());
    }
}
