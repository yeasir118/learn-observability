using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TinyShop.UserService.Infrastructure.Persistence;
using TinyShop.UserService.IntegrationTests.Infrastructure;

namespace TinyShop.UserService.IntegrationTests.Database;

/// <summary>
/// Checks what the migration created in the real database: things the API alone can't show.
/// Every write happens inside a transaction that is rolled back, so other tests still see
/// exactly the five seeded users.
/// </summary>
public class MigrationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task InitialCreate_IsApplied()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<UserDbContext>();

        var applied = await dbContext.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        var pending = await dbContext.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);

        Assert.Contains(applied, migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
        Assert.Empty(pending);
    }

    [Fact]
    public async Task NameLongerThanMaxLength_IsRejectedByTheDatabase()
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertInRolledBackTransactionAsync(new string('a', 101)));

        // 22001 = string_data_right_truncation: varchar(100) refuses the value.
        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, exception.SqlState);
    }

    [Fact]
    public async Task IdentitySequence_ContinuesAfterSeededRows()
    {
        // The seed inserted IDs 123-127 explicitly; the migration then moved the identity
        // sequence past them (setval). Without that, this insert would get ID 1, which would
        // work today but collide once the sequence reached 123.
        // The exact value isn't asserted: sequences don't roll back, so other tests' failed
        // inserts may already have used some numbers.
        var id = await InsertInRolledBackTransactionAsync("Frank");

        Assert.True(id > 127, $"Expected an ID after the seeded rows, got {id}.");
    }

    private async Task<long> InsertInRolledBackTransactionAsync(string name)
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        // As the migrator (the table owner): the read-only app role may not insert at all.
        await using var connection = new NpgsqlConnection(fixture.MigratorConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var command = new NpgsqlCommand(
                "INSERT INTO users (name) VALUES (@name) RETURNING id",
                connection,
                transaction);
            command.Parameters.AddWithValue("name", name);

            return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
        }
        finally
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }
}
