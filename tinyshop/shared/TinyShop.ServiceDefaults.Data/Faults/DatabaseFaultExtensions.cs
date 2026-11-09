using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TinyShop.ServiceDefaults.Abstractions.Faults;

namespace TinyShop.ServiceDefaults.Data.Faults;

/// <summary>
/// Wires the database-delay fault into a service (TS-016): one call registers it, one hands it to
/// EF Core. In a service's Infrastructure:
/// <code>
/// services.AddDatabaseFaults();
/// services.AddDbContext&lt;ProductDbContext&gt;((provider, options) => options
///     .UseNpgsql(connectionString)
///     .AddDatabaseFaults(provider));
/// </code>
/// </summary>
public static class DatabaseFaultExtensions
{
    /// <summary>The options (validated at startup), the startup Warning, and the interceptor.</summary>
    public static IServiceCollection AddDatabaseFaults(this IServiceCollection services)
    {
        services.AddOptions<DatabaseFaultOptions>()
            .BindConfiguration(DatabaseFaultOptions.SectionName)
            .ValidateDataAnnotations()
            // A bad value stops the service at startup instead of silently doing nothing (decision 1).
            .ValidateOnStart();
        services.AddSingleton<IFaultSettings>(provider =>
            provider.GetRequiredService<IOptions<DatabaseFaultOptions>>().Value);

        // One instance for the whole app: it holds no state, and EF Core calls it for every command.
        services.AddSingleton<DatabaseDelayInterceptor>();

        return services;
    }

    /// <summary>
    /// Adds the interceptor to a DbContext. Every command of that DbContext goes through it; it
    /// changes nothing while Faults:DatabaseDelay is zero.
    /// </summary>
    public static DbContextOptionsBuilder AddDatabaseFaults(this DbContextOptionsBuilder options, IServiceProvider provider) =>
        options.AddInterceptors(provider.GetRequiredService<DatabaseDelayInterceptor>());
}
