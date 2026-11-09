using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Npgsql;
using TinyShop.ServiceDefaults.Data.Faults;

namespace TinyShop.ServiceDefaults.UnitTests.Data;

/// <summary>
/// What the database-delay fault does to a command (TS-016, AC3). That Postgres runs the result, and
/// that EF Core still reads it correctly, is tested against a real database in product-service
/// (queries, readiness) and order-service (INSERT ... RETURNING, UPDATE with its row-count check).
/// </summary>
public class DatabaseDelayInterceptorTests
{
    private const string Query = "SELECT p.id, p.name FROM products AS p WHERE p.id = @id";
    private const string Wait = "DO $$BEGIN PERFORM pg_sleep(2); END$$;";

    [Fact]
    public void WithADelay_AppendsAWaitThatReturnsNoResultSet()
    {
        var command = new NpgsqlCommand(Query);

        Interceptor(TimeSpan.FromSeconds(2)).AddDelay(command, CommandSource.LinqQuery);

        // After the query, so its result set and row count keep their position (statement 0).
        Assert.Equal($"{Query};\n{Wait}", command.CommandText);
    }

    [Fact]
    public void ABatchEndingInASemicolon_GetsExactlyOneSeparator()
    {
        // How EF Core writes SaveChanges' statements.
        var command = new NpgsqlCommand("UPDATE orders SET status = @p0 WHERE id = @p1;\n");

        Interceptor(TimeSpan.FromSeconds(2)).AddDelay(command, CommandSource.SaveChanges);

        Assert.Equal($"UPDATE orders SET status = @p0 WHERE id = @p1;\n{Wait}", command.CommandText);
    }

    [Fact]
    public void FractionalDelay_IsWrittenWithADot_WhateverTheCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");   // writes 2.5 as "2,5"
        try
        {
            var command = new NpgsqlCommand(Query);

            Interceptor(TimeSpan.FromMilliseconds(2500)).AddDelay(command, CommandSource.LinqQuery);

            Assert.EndsWith("DO $$BEGIN PERFORM pg_sleep(2.5); END$$;", command.CommandText);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void WithoutADelay_LeavesTheCommandUntouched()
    {
        var command = new NpgsqlCommand(Query);

        Interceptor(TimeSpan.Zero).AddDelay(command, CommandSource.LinqQuery);

        Assert.Same(Query, command.CommandText);
    }

    [Theory]
    [InlineData(CommandSource.LinqQuery)]
    [InlineData(CommandSource.SaveChanges)]
    [InlineData(CommandSource.FromSqlQuery)]
    [InlineData(CommandSource.ExecuteSqlRaw)]
    [InlineData(CommandSource.ExecuteUpdate)]
    [InlineData(CommandSource.ExecuteDelete)]
    public void TheApplicationsCommands_AreDelayed(CommandSource source)
    {
        var command = new NpgsqlCommand(Query);

        Interceptor(TimeSpan.FromSeconds(2)).AddDelay(command, source);

        Assert.EndsWith(Wait, command.CommandText);
    }

    [Theory]
    [InlineData(CommandSource.Unknown)]      // e.g. the readiness check's "SELECT 1" (CanConnect)
    [InlineData(CommandSource.Migrations)]
    [InlineData(CommandSource.ValueGenerator)]
    public void EfCoresOwnCommands_AreNotDelayed(CommandSource source)
    {
        var command = new NpgsqlCommand(Query);

        Interceptor(TimeSpan.FromSeconds(2)).AddDelay(command, source);

        Assert.Same(Query, command.CommandText);
    }

    [Fact]
    public async Task EveryExecutionPath_AddsTheDelay()
    {
        var interceptor = Interceptor(TimeSpan.FromSeconds(1));
        var ct = TestContext.Current.CancellationToken;
        // What EF Core passes for a LINQ query; the interceptor reads only its CommandSource.
        var eventData = new CommandEventData(
            eventDefinition: null!, messageGenerator: (_, _) => string.Empty, connection: null!, command: null!,
            logCommandText: string.Empty, context: null, executeMethod: DbCommandMethod.ExecuteReader,
            commandId: Guid.NewGuid(), connectionId: Guid.NewGuid(), async: true, logParameterValues: false,
            startTime: DateTimeOffset.UtcNow, commandSource: CommandSource.LinqQuery);

        var commands = Enumerable.Range(0, 6).Select(_ => new NpgsqlCommand(Query)).ToArray();
        interceptor.ReaderExecuting(commands[0], eventData, default);
        await interceptor.ReaderExecutingAsync(commands[1], eventData, default, ct);
        interceptor.ScalarExecuting(commands[2], eventData, default);
        await interceptor.ScalarExecutingAsync(commands[3], eventData, default, ct);
        interceptor.NonQueryExecuting(commands[4], eventData, default);
        await interceptor.NonQueryExecutingAsync(commands[5], eventData, default, ct);

        Assert.All(commands, command => Assert.EndsWith("DO $$BEGIN PERFORM pg_sleep(1); END$$;", command.CommandText));
    }

    [Fact]
    public void DescribeActive_ListsTheDelayOnlyWhenItIsOn()
    {
        Assert.Empty(new DatabaseFaultOptions().DescribeActive());
        Assert.Equal(
            ["Faults:DatabaseDelay=00:00:02: every database command waits 2000 ms in Postgres"],
            new DatabaseFaultOptions { DatabaseDelay = TimeSpan.FromSeconds(2) }.DescribeActive());
    }

    private static DatabaseDelayInterceptor Interceptor(TimeSpan delay) =>
        new(Options.Create(new DatabaseFaultOptions { DatabaseDelay = delay }));
}
