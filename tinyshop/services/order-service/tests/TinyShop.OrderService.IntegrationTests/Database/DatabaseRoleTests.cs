using Npgsql;
using TinyShop.OrderService.IntegrationTests.Infrastructure;
using static TinyShop.OrderService.IntegrationTests.Database.SqlHelpers;

namespace TinyShop.OrderService.IntegrationTests.Database;

/// <summary>
/// The least-privilege roles created by infrastructure/postgres/initdb/10-create-service-roles.sh
/// (architecture.md §10, "Database roles"). order-service's app role may read, insert and update
/// orders, and nothing else: orders are never deleted. The migrator owns the schema.
/// </summary>
public class DatabaseRoleTests(PostgresFixture fixture)
{
    [Fact]
    public async Task AppRole_CanInsertReadAndUpdateOrders()
    {
        var status = await ExecuteRolledBackAsync(
            fixture.AppConnectionString,
            $"""
            INSERT INTO orders {PendingOrderColumns} VALUES {PendingOrderValues};
            UPDATE orders SET status = 'FAILED', failure_reason = 'declined' WHERE id = lastval()
            RETURNING status
            """);

        Assert.Equal("FAILED", status);
    }

    [Theory]
    [InlineData("DELETE FROM orders")]
    [InlineData("TRUNCATE orders")]
    [InlineData("CREATE TABLE hack (id int)")]
    [InlineData("CREATE TEMP TABLE hack (id int)")]
    [InlineData("DROP TABLE orders")]
    [InlineData("ALTER TABLE orders ADD COLUMN hack int")]
    public async Task AppRole_CannotDeleteOrChangeTheSchema(string sql)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(fixture.AppConnectionString, sql));

        // 42501 = insufficient_privilege (also how Postgres reports "only the owner may drop or alter").
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }

    [Fact]
    public async Task MigratorRole_OwnsTheTable()
    {
        var owner = await ExecuteScalarAsync<string>(
            fixture.AdminConnectionString,
            "SELECT tableowner FROM pg_tables WHERE schemaname = 'public' AND tablename = 'orders'");

        Assert.Equal("order_migrator", owner);
    }

    [Fact]
    public async Task NeitherServiceRole_IsASuperuser()
    {
        var privileged = await ExecuteScalarAsync<long>(
            fixture.AdminConnectionString,
            "SELECT count(*) FROM pg_roles WHERE rolname IN ('order_migrator', 'order_app') "
            + "AND (rolsuper OR rolcreaterole OR rolcreatedb)");

        Assert.Equal(0, privileged);
    }

    [Fact]
    public async Task TablesCreatedLaterByTheMigrator_GetTheSamePrivileges()
    {
        // Simulates a future migration, with an identity column like orders.id. A unique name
        // keeps this independent of other tests; the table is dropped again at the end.
        var table = $"future_{Guid.NewGuid():N}";

        await ExecuteAsync(fixture.MigratorConnectionString,
            $"CREATE TABLE {table} (id bigint GENERATED ALWAYS AS IDENTITY, value int)");
        try
        {
            // Default privileges granted SELECT, INSERT and UPDATE on the table, and USAGE on its
            // identity sequence (without which the insert couldn't get an ID).
            await ExecuteAsync(fixture.AppConnectionString, $"INSERT INTO {table} (value) VALUES (1)");
            await ExecuteAsync(fixture.AppConnectionString, $"UPDATE {table} SET value = 2");
            Assert.Equal(1, await ExecuteScalarAsync<long>(fixture.AppConnectionString, $"SELECT count(*) FROM {table}"));

            var exception = await Assert.ThrowsAsync<PostgresException>(
                () => ExecuteAsync(fixture.AppConnectionString, $"DELETE FROM {table}"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
        }
        finally
        {
            await ExecuteAsync(fixture.MigratorConnectionString, $"DROP TABLE {table}");
        }
    }
}
