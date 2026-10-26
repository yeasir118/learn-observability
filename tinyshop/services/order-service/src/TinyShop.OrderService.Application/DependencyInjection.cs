using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TinyShop.OrderService.Application.Services;

namespace TinyShop.OrderService.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application layer's services. Implementations stay <c>internal</c>; only their
    /// interfaces are visible to other projects.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // The real clock, unless something (a test) registered another one first.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IOrderPlacement, OrderPlacement>();
        services.AddScoped<IOrderQueries, OrderQueries>();

        return services;
    }
}
