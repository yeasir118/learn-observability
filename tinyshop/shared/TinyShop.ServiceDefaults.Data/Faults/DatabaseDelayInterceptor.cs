using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace TinyShop.ServiceDefaults.Data.Faults;

/// <summary>
/// The database-delay fault (TS-016, decision 6, AC3): adds a wait to every command the application
/// sends through EF Core, so Postgres itself sleeps before the command completes. The time is spent
/// inside the database call: in the command's duration, in Postgres's own statistics, and later in
/// the database span of a trace. A Task.Delay in C# around the call would show up as slow
/// application code instead.
/// </summary>
internal sealed class DatabaseDelayInterceptor(IOptions<DatabaseFaultOptions> options) : DbCommandInterceptor
{
    /// <summary>
    /// The commands the application itself sends: its queries and its writes. Not the ones EF Core
    /// sends for its own purposes (<see cref="CommandSource.Unknown"/>, migrations, ...): among them
    /// the readiness check's "SELECT 1" (Npgsql's CanConnect/Exists), which must stay fast
    /// (decision 9), and the migration job's commands.
    /// </summary>
    private static readonly HashSet<CommandSource> ApplicationCommands =
    [
        CommandSource.LinqQuery,
        CommandSource.SaveChanges,
        CommandSource.FromSqlQuery,
        CommandSource.ExecuteSqlRaw,
        CommandSource.ExecuteUpdate,
        CommandSource.ExecuteDelete,
    ];

    // EF Core sends every command through one of three paths (reader: queries and INSERT ...
    // RETURNING; scalar; non-query: UPDATE, DELETE), each sync and async. All six may get the delay;
    // each returns EF Core's result unchanged, so the command still runs as normal.

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        AddDelay(command, eventData.CommandSource);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        AddDelay(command, eventData.CommandSource);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        AddDelay(command, eventData.CommandSource);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        AddDelay(command, eventData.CommandSource);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        AddDelay(command, eventData.CommandSource);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddDelay(command, eventData.CommandSource);
        return ValueTask.FromResult(result);
    }

    /// <summary>
    /// Appends a statement that waits, to the same command: one round trip, and the command only
    /// completes once Postgres has slept. Two rules decide its shape:
    /// <list type="bullet">
    /// <item><b>After the real statements, not before.</b> EF Core reads the results statement by
    /// statement, by position: result sets, and the affected-row count it checks after an UPDATE.
    /// A statement in front shifts them all; an UPDATE then looks like it changed 0 rows and EF Core
    /// throws DbUpdateConcurrencyException (found by order-service's test).</item>
    /// <item><b>No result set.</b> "SELECT pg_sleep(2)" returns one row, which a reader could hand
    /// on. PERFORM (PL/pgSQL's "run and discard the result") in an anonymous DO block waits without
    /// producing a result set or a row count.</item>
    /// </list>
    /// With the fault off (decision 10), or for a command that isn't the application's, the command
    /// isn't touched at all.
    /// </summary>
    internal void AddDelay(DbCommand command, CommandSource source)
    {
        var delay = options.Value.DatabaseDelay;
        if (delay <= TimeSpan.Zero || !ApplicationCommands.Contains(source))
        {
            return;
        }

        // EF Core's batches already end with ";", a single query doesn't: exactly one either way.
        var statements = command.CommandText.TrimEnd().TrimEnd(';');

        // Invariant culture: "2.5", never "2,5", whatever the machine's locale.
        command.CommandText = string.Create(
            CultureInfo.InvariantCulture,
            $"{statements};\nDO $$BEGIN PERFORM pg_sleep({delay.TotalSeconds}); END$$;");
    }
}
