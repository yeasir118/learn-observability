using Npgsql;
using TinyShop.UserService.IntegrationTests.Infrastructure;

namespace TinyShop.UserService.IntegrationTests.Database;

/// <summary>
/// The least-privilege roles created by infrastructure/postgres/initdb/10-create-service-roles.sh
/// (architecture.md §10, "Database roles"): the app role may only read; the migrator owns the
/// schema. These run against the same script Compose uses, so a broken script fails CI.
/// </summary>
public class DatabaseRoleTests(PostgresFixture fixture)
{
    [Fact]
    public async Task AppRole_CanReadUsers()
    {
        var count = await ExecuteScalarAsync<long>(fixture.AppConnectionString, "SELECT count(*) FROM users");

        Assert.Equal(5, count);
    }

    [Theory]
    [InlineData("INSERT INTO users (name) VALUES ('Mallory')")]
    [InlineData("UPDATE users SET name = 'Mallory' WHERE id = 123")]
    [InlineData("DELETE FROM users WHERE id = 123")]
    [InlineData("CREATE TABLE hack (id int)")]
    [InlineData("CREATE TEMP TABLE hack (id int)")]
    public async Task AppRole_CannotChangeDataOrCreateObjects(string sql)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(fixture.AppConnectionString, sql));

        // 42501 = insufficient_privilege
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Theory]
    [InlineData("DROP TABLE users")]
    [InlineData("ALTER TABLE users ADD COLUMN hack int")]
    public async Task AppRole_CannotChangeTheSchema(string sql)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(fixture.AppConnectionString, sql));

        // Only the owner may drop or alter a table; Postgres reports that as 42501 as well.
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task MigratorRole_OwnsTheTables()
    {
        var owner = await ExecuteScalarAsync<string>(
            fixture.AdminConnectionString,
            "SELECT tableowner FROM pg_tables WHERE schemaname = 'public' AND tablename = 'users'");

        Assert.Equal("user_migrator", owner);
    }

    [Fact]
    public async Task NeitherServiceRole_IsASuperuser()
    {
        var privileged = await ExecuteScalarAsync<long>(
            fixture.AdminConnectionString,
            "SELECT count(*) FROM pg_roles WHERE rolname IN ('user_migrator', 'user_app') "
            + "AND (rolsuper OR rolcreaterole OR rolcreatedb)");

        Assert.Equal(0, privileged);
    }

    [Fact]
    public async Task TablesCreatedLaterByTheMigrator_AreReadOnlyForTheApp()
    {
        // Simulates a future migration. A unique name keeps this independent of other tests;
        // the table is dropped again at the end.
        var table = $"future_{Guid.NewGuid():N}";

        await ExecuteAsync(fixture.MigratorConnectionString, $"CREATE TABLE {table} (id int)");
        try
        {
            // Default privileges granted SELECT automatically, and nothing more.
            Assert.Equal(0, await ExecuteScalarAsync<long>(fixture.AppConnectionString, $"SELECT count(*) FROM {table}"));

            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => ExecuteAsync(fixture.AppConnectionString, $"INSERT INTO {table} VALUES (1)"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
        }
        finally
        {
            await ExecuteAsync(fixture.MigratorConnectionString, $"DROP TABLE {table}");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
