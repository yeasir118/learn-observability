using Npgsql;

namespace TinyShop.OrderService.IntegrationTests.Database;

/// <summary>Raw SQL against the test database, for what the API can't show.</summary>
internal static class SqlHelpers
{
    public static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public static async Task<T> ExecuteScalarAsync<T>(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    /// <summary>
    /// Runs <paramref name="sql"/> in a transaction that is always rolled back, so other tests
    /// never see its rows, and returns the first column of the first row (or null).
    /// </summary>
    public static async Task<object?> ExecuteRolledBackAsync(string connectionString, string sql)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            return await command.ExecuteScalarAsync(cancellationToken);
        }
        finally
        {
            await transaction.RollbackAsync(cancellationToken);
        }
    }

    /// <summary>A valid PENDING order, for INSERT statements.</summary>
    public const string PendingOrderColumns =
        "(user_id, product_id, product_name, unit_price_cents, quantity, total_cents, status, created_at, updated_at)";

    public const string PendingOrderValues =
        "(1, 1, 'Keyboard', 10000, 2, 20000, 'PENDING', now(), now())";
}
