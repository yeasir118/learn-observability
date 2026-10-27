using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyShop.ProductService.Application.Abstractions.Persistence;
using TinyShop.ProductService.Infrastructure.Persistence;
using TinyShop.ProductService.Infrastructure.Persistence.Repositories;

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

        services.AddDbContext<ProductDbContext>(options =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IProductRepository, ProductRepository>();

        // Readiness = "can this service do its job", which for product-service means
        // "can it reach its own database". Liveness checks nothing (contracts.md §5.4).
        // No tag needed: /health/ready runs every registered check (TinyShop.ServiceDefaults).
        services.AddHealthChecks()
            .AddDbContextCheck<ProductDbContext>(name: DatabaseHealthCheckName);

        return services;
    }
}
