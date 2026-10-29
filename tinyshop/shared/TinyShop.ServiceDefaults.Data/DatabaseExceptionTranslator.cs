using Npgsql;
using TinyShop.ServiceDefaults.Abstractions;

namespace TinyShop.ServiceDefaults.Data;

/// <summary>
/// Turns Npgsql-specific failures into <see cref="DataStoreUnavailableException"/>, so nothing
/// outside a service's Infrastructure layer needs to know which database driver is in use.
/// Wrap every database call in a repository: <c>DatabaseExceptionTranslator.ExecuteAsync(() => ...)</c>.
/// </summary>
public static class DatabaseExceptionTranslator
{
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            // Only logged, never sent to clients. The log line already names the service (each
            // container logs separately) and the inner exception names the host.
            throw new DataStoreUnavailableException("The database is unavailable.", exception);
        }
    }

    /// <summary>
    /// "Unavailable" means the failure is transient: connection refused, timeout, server shutting down.
    /// Retrying later could succeed, so it maps to 503. A non-transient error, such as a syntax error
    /// or a missing table, is a bug in this service and is deliberately not translated: it becomes a 500.
    /// </summary>
    private static bool IsUnavailable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException npgsqlException)
            {
                return npgsqlException.IsTransient;
            }
        }

        return false;
    }
}
