using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TinyShop.OrderService.Infrastructure.Persistence;
using TinyShop.OrderService.IntegrationTests.Infrastructure;
using static TinyShop.OrderService.IntegrationTests.Database.SqlHelpers;

namespace TinyShop.OrderService.IntegrationTests.Database;

/// <summary>
/// Checks what the migration created in the real database: things the API alone can't show.
/// Every write happens inside a transaction that is rolled back.
/// </summary>
public class MigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task InitialCreate_IsApplied()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrderDbContext>();

        var applied = await dbContext.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        var pending = await dbContext.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(applied, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
        Assert.Empty(pending);
    }

    [Fact]
    public async Task Ids_StartAt1001()
    {
        var id = (long)(await ExecuteRolledBackAsync(fixture.MigratorConnectionString,
            $"INSERT INTO orders {PendingOrderColumns} VALUES {PendingOrderValues} RETURNING id"))!;

        Assert.True(id >= 1001, $"Expected an ID of at least 1001, got {id}.");
    }

    [Fact]
    public async Task Ids_AreGeneratedAlways_SoAnExplicitIdIsRejected()
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteRolledBackAsync(
            fixture.MigratorConnectionString,
            $"INSERT INTO orders (id, {PendingOrderColumns[1..]} VALUES (1, {PendingOrderValues[1..]}"));

        // 428C9 = generated_always (contracts.md §1.1: GENERATED ALWAYS AS IDENTITY).
        Assert.Equal("428C9", exception.SqlState);
    }

    [Theory]
    [InlineData("quantity = 0, total_cents = 0", "ck_orders_quantity_range")]
    [InlineData("quantity = 101, total_cents = 1010000", "ck_orders_quantity_range")]
    [InlineData("unit_price_cents = 0, total_cents = 0", "ck_orders_unit_price_cents_positive")]
    [InlineData("total_cents = 1", "ck_orders_total_cents")]
    [InlineData("status = 'SHIPPED'", "ck_orders_status")]
    [InlineData("status = 'paid', transaction_id = 'txn_1'", "ck_orders_status")]
    [InlineData("status = 'PAID'", "ck_orders_transaction_id_when_paid")]
    [InlineData("transaction_id = 'txn_1'", "ck_orders_transaction_id_when_paid")]
    [InlineData("status = 'FAILED'", "ck_orders_failure_reason_when_failed")]
    [InlineData("failure_reason = 'declined'", "ck_orders_failure_reason_when_failed")]
    public async Task CheckConstraints_RejectInconsistentOrders(string change, string constraint)
    {
        // Insert a valid order, then break exactly one rule.
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteRolledBackAsync(
            fixture.MigratorConnectionString,
            $"""
            INSERT INTO orders {PendingOrderColumns} VALUES {PendingOrderValues};
            UPDATE orders SET {change} WHERE id = lastval()
            """));

        // 23514 = check_violation: the database itself refused the row, whoever sent it.
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(constraint, exception.ConstraintName);
    }
}
