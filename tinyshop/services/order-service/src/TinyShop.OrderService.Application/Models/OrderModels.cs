namespace TinyShop.OrderService.Application.Models;

/// <summary>An order as the application exposes it (contracts.md §3).</summary>
public sealed record OrderDto(
    long Id,
    long UserId,
    long ProductId,
    string ProductName,
    long UnitPriceCents,
    int Quantity,
    long TotalCents,
    string Status,
    string? FailureReason,
    string? TransactionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A request to place an order. The Api validates the values before this is created.</summary>
public sealed record PlaceOrderCommand(long UserId, long ProductId, int Quantity);

/// <summary>
/// Every outcome of placing an order that the caller must handle differently (contracts.md §6).
/// Failures of user-service, product-service and the order database are not results: they're
/// exceptions, handled the same way wherever they happen.
/// </summary>
public abstract record PlaceOrderResult
{
    private PlaceOrderResult()
    {
    }

    /// <summary>The order exists and payment produced an answer: approved (Paid) or declined (Failed). 201.</summary>
    public sealed record Placed(OrderDto Order) : PlaceOrderResult;

    /// <summary>user-service says the user doesn't exist. 422, no order created.</summary>
    public sealed record UserNotFound(long UserId) : PlaceOrderResult;

    /// <summary>product-service says the product doesn't exist. 422, no order created.</summary>
    public sealed record ProductNotFound(long ProductId) : PlaceOrderResult;

    /// <summary>payment-service failed or couldn't be reached; the order is Failed. 502, with the order's ID.</summary>
    public sealed record PaymentUnavailable(OrderDto Order) : PlaceOrderResult;

    /// <summary>The payment call timed out; the order stays Pending. 504, with the order's ID.</summary>
    public sealed record PaymentTimedOut(OrderDto Order) : PlaceOrderResult;

    /// <summary>
    /// The order's new status (Paid or Failed) couldn't be saved: order-db failed after the order
    /// row was created, so the stored order stays Pending. 503, with the order's ID.
    /// </summary>
    public sealed record StatusNotRecorded(long OrderId) : PlaceOrderResult;
}
