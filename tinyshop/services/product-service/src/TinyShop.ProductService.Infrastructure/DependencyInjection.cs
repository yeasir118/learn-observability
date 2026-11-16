using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyShop.ProductService.Application.Abstractions.Persistence;
using TinyShop.ProductService.Infrastructure.Persistence;
using TinyShop.ProductService.Infrastructure.Persistence.Repositories;
using TinyShop.ServiceDefaults.Data.Faults;

namespace TinyShop.ProductService.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Name of the configuration entry under ConnectionStrings.</summary>
    private const string ConnectionStringName = "ProductDb";

    /// <summary>Name of the database health check, as it appears in health reports.</summary>
    private const string DatabaseHealthCheckName = "product-db";

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
        services.AddDbContext<ProductDbContext>((provider, options) =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .AddDatabaseFaults(provider));

        services.AddScoped<IProductRepository, ProductRepository>();

        // Readiness = "can this service do its job", which for product-service means
        // "can it reach its own database". Liveness checks nothing (contracts.md §5.4).
        // No tag needed: /health/ready runs every registered check (TinyShop.ServiceDefaults).
        services.AddHealthChecks()
            .AddDbContextCheck<ProductDbContext>(name: DatabaseHealthCheckName);

        return services;
    }
}
