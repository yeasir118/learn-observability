using Microsoft.EntityFrameworkCore;
using TinyShop.ProductService.Infrastructure.Persistence;
using TinyShop.Testing;

// One fixture instance for the whole test assembly: one container per test run, shared by
// every test class that asks for PostgresFixture in its constructor.
[assembly: AssemblyFixture(typeof(TinyShop.ProductService.IntegrationTests.Infrastructure.PostgresFixture))]

namespace TinyShop.ProductService.IntegrationTests.Infrastructure;

/// <summary>
/// A throwaway PostgreSQL set up like Compose's product-db (see <see cref="PostgresFixtureBase"/>).
/// product_app may only read; the app under test connects as it, like product-service in Compose.
/// </summary>
public sealed class PostgresFixture() : PostgresFixtureBase(
    database: "tinyshop_products",
    rolePrefix: "product",
    appTablePrivileges: "SELECT")
{
    public ProductServiceFactory Factory { get; private set; } = null!;

    protected override async Task MigrateAsync(string migratorConnectionString)
    {
        var options = new DbContextOptionsBuilder<ProductDbContext>()
            .UseNpgsql(migratorConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var dbContext = new ProductDbContext(options);
        await dbContext.Database.MigrateAsync();
    }

    protected override Task OnReadyAsync()
    {
        Factory = new ProductServiceFactory(AppConnectionString);
        return Task.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await base.DisposeAsync();
    }
}
