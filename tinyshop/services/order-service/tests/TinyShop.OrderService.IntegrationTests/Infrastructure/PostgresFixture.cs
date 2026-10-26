using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using TinyShop.OrderService.Infrastructure.Persistence;

// One fixture instance for the whole test assembly: one container per test run, shared by
// every test class that asks for PostgresFixture in its constructor.
[assembly: AssemblyFixture(typeof(TinyShop.OrderService.IntegrationTests.Infrastructure.PostgresFixture))]

namespace TinyShop.OrderService.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a throwaway PostgreSQL in Docker, set up like Compose's databases: the same image, the
/// same init script, the same roles (architecture.md §10, "Database roles"). Migrations run as the
/// migrator role; the app under test connects as the app role, which may read, insert and update
/// orders but never delete them. Disposed (container removed) when the run ends.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string Database = "tinyshop_orders";
    private const string MigratorPassword = "test-migrator";
    private const string AppPassword = "test-app";

    // The same image as tinyshop/infrastructure/compose/compose.yaml, so tests run against the
    // database version the service really uses.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase(Database)
        .WithUsername("postgres")
        .WithPassword("test-admin")
        // The environment the init script reads. Unlike the other services, the app role writes:
        // orders are created and their status updated (TS-013).
        .WithEnvironment("DB_ROLE_PREFIX", "order")
        .WithEnvironment("MIGRATOR_PASSWORD", MigratorPassword)
        .WithEnvironment("APP_PASSWORD", AppPassword)
        .WithEnvironment("APP_TABLE_PRIVILEGES", "SELECT, INSERT, UPDATE")
        // Copied in without the executable bit, so the image *sources* the script, which is also
        // what happens in a Linux checkout where the bit isn't set.
        .WithResourceMapping(
            new FileInfo(Path.Combine(AppContext.BaseDirectory, "initdb", "10-create-service-roles.sh")),
            "/docker-entrypoint-initdb.d/")
        .Build();

    public OrderServiceFactory Factory { get; private set; } = null!;

    /// <summary>The superuser. Only for tests that deliberately bypass the roles.</summary>
    public string AdminConnectionString => _container.GetConnectionString();

    /// <summary>order_migrator: owns the schema, as used by the migration job.</summary>
    public string MigratorConnectionString => WithCredentials("order_migrator", MigratorPassword);

    /// <summary>order_app: SELECT, INSERT, UPDATE, as used by order-service.</summary>
    public string AppConnectionString => WithCredentials("order_app", AppPassword);

    /// <summary>The host and port Postgres is published on (a random free port on the host).</summary>
    public (string Host, int Port) Endpoint => (_container.Hostname, _container.GetMappedPublicPort(5432));

    public async ValueTask InitializeAsync()
    {
        // Testcontainers maps the container's 5432 to a random free host port, so parallel runs
        // and a local Postgres on 5432 never collide. The connection strings include that port.
        await _container.StartAsync();

        // Apply the real migrations once, before any test runs, as the migrator role: the same
        // role and the same migrations the migration job will use. This also proves that
        // InitialCreate works on a real, empty database with least-privilege roles.
        var migratorOptions = new DbContextOptionsBuilder<OrderDbContext>();
        migratorOptions.UseOrderDatabase(MigratorConnectionString);
        await using (var dbContext = new OrderDbContext(migratorOptions.Options))
        {
            await dbContext.Database.MigrateAsync();
        }

        Factory = new OrderServiceFactory(AppConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    private string WithCredentials(string username, string password) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = username,
            Password = password,
        }.ConnectionString;
}
