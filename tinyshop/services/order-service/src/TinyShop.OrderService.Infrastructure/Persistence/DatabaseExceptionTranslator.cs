using Npgsql;
using TinyShop.OrderService.Application.Common.Exceptions;

namespace TinyShop.OrderService.Infrastructure.Persistence;

/// <summary>
/// Turns Npgsql-specific failures into Application exceptions, so nothing outside Infrastructure
/// needs to know which database driver is in use.
/// </summary>
internal static class DatabaseExceptionTranslator
{
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            throw new DataStoreUnavailableException("The order database is unavailable.", exception);
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
