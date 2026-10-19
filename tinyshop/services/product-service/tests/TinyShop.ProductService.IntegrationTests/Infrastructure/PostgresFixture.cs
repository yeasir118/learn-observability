using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using TinyShop.ProductService.Infrastructure.Persistence;

// One fixture instance for the whole test assembly: one container per test run, shared by
// every test class that asks for PostgresFixture in its constructor.
[assembly: AssemblyFixture(typeof(TinyShop.ProductService.IntegrationTests.Infrastructure.PostgresFixture))]

namespace TinyShop.ProductService.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a throwaway PostgreSQL in Docker, applies the service's real EF Core migrations to it,
/// and hosts product-service against it. Disposed (container removed) when the run ends.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    // The same image as tinyshop/infrastructure/compose/compose.yaml, so tests run against the
    // database version the service really uses.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine").Build();

    public ProductServiceFactory Factory { get; private set; } = null!;

    /// <summary>For tests that talk to the database directly rather than through the API.</summary>
    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        // Testcontainers maps the container's 5432 to a random free host port, so parallel runs
        // and a local Postgres on 5432 never collide. GetConnectionString() includes that port.
        await _container.StartAsync();

        Factory = new ProductServiceFactory(_container.GetConnectionString());

        // Apply the real migrations (schema and seed data) exactly once, before any test runs.
        // This also proves that InitialCreate works on a real, empty database.
        await using var scope = Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }
}
