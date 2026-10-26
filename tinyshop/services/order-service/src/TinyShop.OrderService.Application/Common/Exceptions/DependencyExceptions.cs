namespace TinyShop.OrderService.Application.Common.Exceptions;

/// <summary>
/// user-service or product-service failed (5xx) or couldn't be reached (connection refused,
/// name not found). Retrying later could succeed. The Api answers 502 dependency_failed.
/// </summary>
public sealed class DependencyUnavailableException(string dependency, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>Which dependency, for logs only: never shown to clients (contracts.md §2.1).</summary>
    public string Dependency { get; } = dependency;
}

/// <summary>
/// user-service or product-service didn't answer in time. The Api answers 504 dependency_timeout.
/// </summary>
public sealed class DependencyTimeoutException(string dependency, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Dependency { get; } = dependency;
}

/// <summary>
/// payment-service failed or couldn't be reached, so nothing was charged: the order becomes Failed.
/// </summary>
public sealed class PaymentUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// The payment call ran past its deadline. Whether the payment happened is unknown, so the order
/// stays Pending.
/// </summary>
public sealed class PaymentTimeoutException(string message, Exception? innerException = null)
    : Exception(message, innerException);
