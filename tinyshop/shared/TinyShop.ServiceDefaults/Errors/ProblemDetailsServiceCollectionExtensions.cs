using Microsoft.Extensions.DependencyInjection;

namespace TinyShop.ServiceDefaults.Errors;

public static class ProblemDetailsServiceCollectionExtensions
{
    /// <summary>
    /// Makes every error response Problem Details in the contracts.md §2.1 shape.
    /// <paramref name="addServiceCodes"/> adds the codes only this service returns, for example
    /// <c>catalog => catalog.Add("product_not_found", "Product not found")</c>.
    /// </summary>
    public static IServiceCollection AddDefaultProblemDetails(
        this IServiceCollection services,
        Action<ErrorCatalog> addServiceCodes)
    {
        // Built once, here: the catalog is fixed at startup, and a conflicting code fails now
        // rather than on the first error response.
        var catalog = new ErrorCatalog();
        addServiceCodes(catalog);

        var customizer = new ProblemDetailsCustomizer(catalog);
        services.AddProblemDetails(options => options.CustomizeProblemDetails = customizer.Customize);

        return services;
    }
}
