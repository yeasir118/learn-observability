using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.OrderService.Api.Errors;

/// <summary>
/// The error codes only order-service returns (contracts.md §2.3, §6). The codes every service
/// shares (validation_failed, not_found, ...) are in TinyShop.ServiceDefaults' <see cref="ErrorCodes"/>.
/// </summary>
internal static class OrderErrorCodes
{
    public const string UserNotFound = "user_not_found";
    public const string ProductNotFound = "product_not_found";
    public const string OrderNotFound = "order_not_found";
    public const string DependencyFailed = "dependency_failed";
    public const string DependencyTimeout = "dependency_timeout";
    public const string PaymentUnavailable = "payment_unavailable";
    public const string PaymentTimeout = "payment_timeout";

    /// <summary>Adds this service's codes to its error catalog (passed to AddDefaultProblemDetails).</summary>
    public static void Register(ErrorCatalog catalog) =>
        catalog
            .Add(UserNotFound, "User not found")
            .Add(ProductNotFound, "Product not found")
            .Add(OrderNotFound, "Order not found")
            // A framework-generated 502 or 504 here can only mean a dependency failed.
            .Add(DependencyFailed, "Dependency failed", forStatus: StatusCodes.Status502BadGateway)
            .Add(DependencyTimeout, "Dependency timeout", forStatus: StatusCodes.Status504GatewayTimeout)
            .Add(PaymentUnavailable, "Payment unavailable")
            .Add(PaymentTimeout, "Payment timeout");
}
