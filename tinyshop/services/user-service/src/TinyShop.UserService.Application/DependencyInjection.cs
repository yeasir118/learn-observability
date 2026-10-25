using Microsoft.Extensions.DependencyInjection;
using TinyShop.UserService.Application.Services;

namespace TinyShop.UserService.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Application layer's services. Implementations stay <c>internal</c>; only their
    /// interfaces are visible to other projects.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserDirectoryService, UserDirectoryService>();

        return services;
    }
}
