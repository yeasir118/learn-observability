using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;
using TinyShop.UserService.Infrastructure.Persistence;

// One fixture instance for the whole test assembly: one container per test run, shared by
// every test class that asks for PostgresFixture in its constructor.
[assembly: AssemblyFixture(typeof(TinyShop.UserService.IntegrationTests.Infrastructure.PostgresFixture))]

namespace TinyShop.UserService.IntegrationTests.Infrastructure;

/// <summary>
/// Starts a throwaway PostgreSQL in Docker, set up exactly like Compose's user-db: the same
/// image, the same init script, the same three roles (architecture.md §10, "Database roles").
/// Migrations run as the migrator role; the app under test connects as the read-only app role.
/// Disposed (container removed) when the run ends.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string Database = "tinyshop_users";
    private const string MigratorPassword = "test-migrator";
    private const string AppPassword = "test-app";

    // The same image as tinyshop/infrastructure/compose/compose.yaml, so tests run against the
    // database version the service really uses.
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase(Database)
        .WithUsername("postgres")
        .WithPassword("test-admin")
        // The same environment Compose gives user-db, read by the init script.
        .WithEnvironment("DB_ROLE_PREFIX", "user")
        .WithEnvironment("MIGRATOR_PASSWORD", MigratorPassword)
        .WithEnvironment("APP_PASSWORD", AppPassword)
        .WithEnvironment("APP_TABLE_PRIVILEGES", "SELECT")
        // Copied in without the executable bit, so the image *sources* the script, which is also
        // what happens in a Linux checkout where the bit isn't set.
        .WithResourceMapping(
            new FileInfo(Path.Combine(AppContext.BaseDirectory, "initdb", "10-create-service-roles.sh")),
            "/docker-entrypoint-initdb.d/")
        .Build();

    public UserServiceFactory Factory { get; private set; } = null!;

    /// <summary>The superuser. Only for tests that deliberately bypass the roles.</summary>
    public string AdminConnectionString => _container.GetConnectionString();

    /// <summary>user_migrator: owns the schema, as used by the migration job.</summary>
    public string MigratorConnectionString => WithCredentials("user_migrator", MigratorPassword);

    /// <summary>user_app: read-only, as used by user-service.</summary>
    public string AppConnectionString => WithCredentials("user_app", AppPassword);

    public async ValueTask InitializeAsync()
    {
        // Testcontainers maps the container's 5432 to a random free host port, so parallel runs
        // and a local Postgres on 5432 never collide. The connection strings include that port.
        await _container.StartAsync();

        // Apply the real migrations once, before any test runs, as the migrator role: the same
        // role and the same migrations the user-db-migrate job uses. This also proves that
        // InitialCreate works on a real, empty database with least-privilege roles.
        var migratorOptions = new DbContextOptionsBuilder<UserDbContext>();
        migratorOptions.UseUserDatabase(MigratorConnectionString);
        await using (var dbContext = new UserDbContext(migratorOptions.Options))
        {
            await dbContext.Database.MigrateAsync();
        }

        // The app under test connects as the read-only role, like user-service in Compose.
        Factory = new UserServiceFactory(AppConnectionString);
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
