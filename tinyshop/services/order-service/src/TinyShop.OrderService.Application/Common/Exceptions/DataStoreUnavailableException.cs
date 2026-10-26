namespace TinyShop.OrderService.Application.Common.Exceptions;

/// <summary>
/// The data store couldn't be reached (connection refused, timeout, server shutting down).
/// Infrastructure translates provider-specific exceptions into this one, so outer layers can react
/// to "storage is unavailable" without knowing which database or driver is in use.
/// </summary>
public sealed class DataStoreUnavailableException : Exception
{
    public DataStoreUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
