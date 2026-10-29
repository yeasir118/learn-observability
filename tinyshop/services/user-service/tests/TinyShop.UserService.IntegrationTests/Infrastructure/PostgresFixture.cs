using Microsoft.EntityFrameworkCore;
using TinyShop.Testing;
using TinyShop.UserService.Infrastructure.Persistence;

// One fixture instance for the whole test assembly: one container per test run, shared by
// every test class that asks for PostgresFixture in its constructor.
[assembly: AssemblyFixture(typeof(TinyShop.UserService.IntegrationTests.Infrastructure.PostgresFixture))]

namespace TinyShop.UserService.IntegrationTests.Infrastructure;

/// <summary>
/// A throwaway PostgreSQL set up like Compose's user-db (see <see cref="PostgresFixtureBase"/>).
/// user_app may only read; the app under test connects as it, like user-service in Compose.
/// </summary>
public sealed class PostgresFixture() : PostgresFixtureBase(
    database: "tinyshop_users",
    rolePrefix: "user",
    appTablePrivileges: "SELECT")
{
    public UserServiceFactory Factory { get; private set; } = null!;

    protected override async Task MigrateAsync(string migratorConnectionString)
    {
        var options = new DbContextOptionsBuilder<UserDbContext>();
        options.UseUserDatabase(migratorConnectionString);
        await using var dbContext = new UserDbContext(options.Options);
        await dbContext.Database.MigrateAsync();
    }

    protected override Task OnReadyAsync()
    {
        Factory = new UserServiceFactory(AppConnectionString);
        return Task.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await base.DisposeAsync();
    }
}
