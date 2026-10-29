using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace TinyShop.Testing;

/// <summary>
/// Starts a throwaway PostgreSQL in Docker, set up like Compose's databases: the same image, the
/// same init script, the same three roles (architecture.md §10, "Database roles"). Migrations run as
/// the migrator role; the app under test connects as the app role. Disposed (container removed)
/// when the run ends.
/// </summary>
/// <remarks>
/// A service's fixture derives from this, supplies its database and roles, applies its migrations
/// in <see cref="MigrateAsync"/> and creates its app in <see cref="OnReadyAsync"/>. Register the
/// derived fixture once per test assembly with <c>[assembly: AssemblyFixture(typeof(...))]</c>.
/// </remarks>
public abstract class PostgresFixtureBase : IAsyncLifetime
{
    private const string MigratorPassword = "test-migrator";
    private const string AppPassword = "test-app";

    private readonly string _rolePrefix;
    private readonly PostgreSqlContainer _container;

    /// <param name="database">The database name, as in Compose (<c>tinyshop_products</c>).</param>
    /// <param name="rolePrefix">Prefix of the roles the init script creates (<c>product</c>:
    /// <c>product_migrator</c>, <c>product_app</c>).</param>
    /// <param name="appTablePrivileges">What the app role may do on tables, as in Compose
    /// (<c>SELECT</c>, or <c>SELECT, INSERT, UPDATE</c>).</param>
    protected PostgresFixtureBase(string database, string rolePrefix, string appTablePrivileges)
    {
        _rolePrefix = rolePrefix;

        // The same image as tinyshop/infrastructure/compose/compose.yaml, so tests run against the
        // database version the services really use.
        _container = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase(database)
            .WithUsername("postgres")
            .WithPassword("test-admin")
            // The same environment Compose gives the service's database, read by the init script.
            .WithEnvironment("DB_ROLE_PREFIX", rolePrefix)
            .WithEnvironment("MIGRATOR_PASSWORD", MigratorPassword)
            .WithEnvironment("APP_PASSWORD", AppPassword)
            .WithEnvironment("APP_TABLE_PRIVILEGES", appTablePrivileges)
            // Copied in without the executable bit, so the image *sources* the script, which is also
            // what happens in a Linux checkout where the bit isn't set. TinyShop.Testing.csproj
            // copies the script next to the test binaries.
            .WithResourceMapping(
                new FileInfo(Path.Combine(AppContext.BaseDirectory, "initdb", "10-create-service-roles.sh")),
                "/docker-entrypoint-initdb.d/")
            .Build();
    }

    /// <summary>The superuser. Only for tests that deliberately bypass the roles.</summary>
    public string AdminConnectionString => _container.GetConnectionString();

    /// <summary>&lt;prefix&gt;_migrator: owns the schema, as used by the migration job.</summary>
    public string MigratorConnectionString => WithCredentials($"{_rolePrefix}_migrator", MigratorPassword);

    /// <summary>&lt;prefix&gt;_app: what the service connects as, with the privileges passed in.</summary>
    public string AppConnectionString => WithCredentials($"{_rolePrefix}_app", AppPassword);

    /// <summary>The host and port Postgres is published on (a random free port on the host).</summary>
    public (string Host, int Port) Endpoint => (_container.Hostname, _container.GetMappedPublicPort(5432));

    public async ValueTask InitializeAsync()
    {
        // Testcontainers maps the container's 5432 to a random free host port, so parallel runs
        // and a local Postgres on 5432 never collide. The connection strings include that port.
        await _container.StartAsync();

        // Apply the real migrations once, before any test runs, as the migrator role: the same
        // role and the same migrations the migration job uses. This also proves that
        // InitialCreate works on a real, empty database with least-privilege roles.
        await MigrateAsync(MigratorConnectionString);

        await OnReadyAsync();
    }

    /// <summary>Removes the container. A derived fixture disposes its app first, then calls this.</summary>
    public virtual async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Applies the service's migrations, connected as the migrator role.</summary>
    protected abstract Task MigrateAsync(string migratorConnectionString);

    /// <summary>Runs once the database is migrated: typically creates the app under test.</summary>
    protected virtual Task OnReadyAsync() => Task.CompletedTask;

    private string WithCredentials(string username, string password) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = username,
            Password = password,
        }.ConnectionString;
}
