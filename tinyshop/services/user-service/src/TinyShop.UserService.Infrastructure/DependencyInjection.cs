using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyShop.ServiceDefaults.Data.Faults;
using TinyShop.UserService.Application.Abstractions.Persistence;
using TinyShop.UserService.Infrastructure.Persistence;
using TinyShop.UserService.Infrastructure.Persistence.Repositories;

namespace TinyShop.UserService.Infrastructure;

public static class DependencyInjection
{
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

        // The database-delay fault (TS-016): every command of this DbContext goes through the
        // interceptor, which does nothing unless Faults:DatabaseDelay is set (docs/faults.md).
        services.AddDatabaseFaults();
        services.AddDbContext<UserDbContext>((provider, options) =>
            options.UseUserDatabase(connectionString).AddDatabaseFaults(provider));

        services.AddScoped<IUserRepository, UserRepository>();

        // Readiness = "can this service do its job", which for user-service means
        // "can it reach its own database". Liveness checks nothing (contracts.md §5.4).
        // No tag needed: /health/ready runs every registered check (TinyShop.ServiceDefaults).
        services.AddHealthChecks()
            .AddDbContextCheck<UserDbContext>(name: DatabaseHealthCheckName);

        return services;
    }
}
