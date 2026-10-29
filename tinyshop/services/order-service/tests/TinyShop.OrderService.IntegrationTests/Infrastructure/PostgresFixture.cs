using Microsoft.EntityFrameworkCore;
using TinyShop.OrderService.Infrastructure.Persistence;
using TinyShop.Testing;

// One fixture instance for the whole test assembly: one container per test run, shared by
// every test class that asks for PostgresFixture in its constructor.
[assembly: AssemblyFixture(typeof(TinyShop.OrderService.IntegrationTests.Infrastructure.PostgresFixture))]

namespace TinyShop.OrderService.IntegrationTests.Infrastructure;

/// <summary>
/// A throwaway PostgreSQL set up like Compose's order-db (see <see cref="PostgresFixtureBase"/>).
/// Unlike the other services, order_app writes: orders are created and their status updated, but
/// never deleted (TS-013). The app under test connects as it, like order-service in Compose.
/// </summary>
public sealed class PostgresFixture() : PostgresFixtureBase(
    database: "tinyshop_orders",
    rolePrefix: "order",
    appTablePrivileges: "SELECT, INSERT, UPDATE")
{
    public OrderServiceFactory Factory { get; private set; } = null!;

    protected override async Task MigrateAsync(string migratorConnectionString)
    {
        var options = new DbContextOptionsBuilder<OrderDbContext>();
        options.UseOrderDatabase(migratorConnectionString);
        await using var dbContext = new OrderDbContext(options.Options);
        await dbContext.Database.MigrateAsync();
    }

    protected override Task OnReadyAsync()
    {
        Factory = new OrderServiceFactory(AppConnectionString);
        return Task.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await base.DisposeAsync();
    }
}
