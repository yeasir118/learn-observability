using Microsoft.Extensions.DependencyInjection;
using TinyShop.ProductService.Application.Services;

namespace TinyShop.ProductService.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application layer's services. Implementations stay <c>internal</c>; only their
    /// interfaces are visible to other projects.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IProductCatalogService, ProductCatalogService>();

        return services;
    }
}
