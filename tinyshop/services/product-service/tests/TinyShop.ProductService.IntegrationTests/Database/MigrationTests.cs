using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TinyShop.ProductService.Infrastructure.Persistence;
using TinyShop.ProductService.IntegrationTests.Infrastructure;

namespace TinyShop.ProductService.IntegrationTests.Database;

/// <summary>
/// Checks what the migration created in the real database: things the API alone can't show.
/// Every write happens inside a transaction that is rolled back, so other tests still see
/// exactly the five seeded products.
/// </summary>
public class MigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task InitialCreate_IsApplied()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

        var applied = await dbContext.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        var pending = await dbContext.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(applied, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
        Assert.Empty(pending);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CheckConstraint_RejectsNonPositivePrice(long priceCents)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertInRolledBackTransactionAsync("Invalid", priceCents));

        // 23514 = check_violation: the database itself refused the row, whoever sent it.
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_products_price_cents_positive", exception.ConstraintName);
    }

    [Fact]
    public async Task IdentitySequence_ContinuesAfterSeededRows()
    {
        // The seed inserted IDs 1-5 explicitly; the migration then moved the identity sequence
        // past them (setval). Without that, this insert would get ID 1 and fail on the primary key.
        // The exact value isn't asserted: sequences don't roll back, so other tests' failed
        // inserts may already have used some numbers.
        var id = await InsertInRolledBackTransactionAsync("Webcam", 7_000);

        Assert.True(id > 5, $"Expected an ID after the seeded rows, got {id}.");
    }

    private async Task<long> InsertInRolledBackTransactionAsync(string name, long priceCents)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var command = new NpgsqlCommand(
                "INSERT INTO products (name, price_cents) VALUES (@name, @price) RETURNING id",
                connection,
                transaction);
            command.Parameters.AddWithValue("name", name);
            command.Parameters.AddWithValue("price", priceCents);

            return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        }
        finally
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }
}
