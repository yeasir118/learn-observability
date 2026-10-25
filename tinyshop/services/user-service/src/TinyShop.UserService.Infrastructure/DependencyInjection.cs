using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyShop.UserService.Application.Abstractions.Persistence;
using TinyShop.UserService.Infrastructure.Persistence;
using TinyShop.UserService.Infrastructure.Persistence.Repositories;

namespace TinyShop.UserService.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Health-check tag for checks that decide readiness (see contracts.md §5.4).</summary>
    public const string ReadyTag = "ready";

    /// <summary>Name of the configuration entry under ConnectionStrings.</summary>
    private const string ConnectionStringName = "UserDb";

    /// <summary>Name of the database health check, as it appears in health reports.</summary>
    private const string DatabaseHealthCheckName = "user-db";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Fail at startup, with a clear message, rather than on the first request.
        var connectionString = configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is missing from configuration "
                + $"(key ConnectionStrings:{ConnectionStringName}).");

        services.AddDbContext<UserDbContext>(options => options.UseUserDatabase(connectionString));

        services.AddScoped<IUserRepository, UserRepository>();

        // Readiness = "can this service do its job", which for user-service means
        // "can it reach its own database". Liveness checks nothing (contracts.md §5.4).
        services.AddHealthChecks()
            .AddDbContextCheck<UserDbContext>(name: DatabaseHealthCheckName, tags: [ReadyTag]);

        return services;
    }
}
