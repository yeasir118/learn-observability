using TinyShop.OrderService.Domain.Enums;

namespace TinyShop.OrderService.Domain.Entities;

/// <summary>
/// An order for one product (contracts.md §3). The product's name and price are copied in when
/// the order is placed: an order records what was bought at the price paid, so a later price
/// change in product-service never changes existing orders.
/// </summary>
public sealed class Order
{
    public const int MinQuantity = 1;
    public const int MaxQuantity = 100;
    public const int ProductNameMaxLength = 100;
    public const int FailureReasonMaxLength = 100;
    public const int TransactionIdMaxLength = 100;

    /// <summary>Used by EF Core to rebuild orders that already exist in the database.</summary>
    private Order()
    {
        ProductName = string.Empty;
    }

    private Order(long userId, long productId, string productName, long unitPriceCents, int quantity, DateTimeOffset now)
    {
        UserId = userId;
        ProductId = productId;
        ProductName = productName;
        UnitPriceCents = unitPriceCents;
        Quantity = quantity;
        // checked: an overflow throws instead of silently wrapping to a negative total.
        TotalCents = checked(unitPriceCents * quantity);
        Status = OrderStatus.Pending;
        CreatedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Assigned by the database.</summary>
    public long Id { get; private set; }

    public long UserId { get; private set; }

    public long ProductId { get; private set; }

    /// <summary>Snapshot of the product's name when the order was placed.</summary>
    public string ProductName { get; private set; }

    /// <summary>Snapshot of the product's price when the order was placed.</summary>
    public long UnitPriceCents { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>UnitPriceCents × Quantity, computed here, never accepted from a caller.</summary>
    public long TotalCents { get; private set; }

    public OrderStatus Status { get; private set; }

    /// <summary>Set only when Status is Failed.</summary>
    public string? FailureReason { get; private set; }

    /// <summary>Set only when Status is Paid.</summary>
    public string? TransactionId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Creates a new order in the Pending state, before payment is attempted.</summary>
    public static Order Place(
        long userId, long productId, string productName, long unitPriceCents, int quantity, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(productId);
        ArgumentException.ThrowIfNullOrWhiteSpace(productName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(productName.Length, ProductNameMaxLength, nameof(productName));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unitPriceCents);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, MinQuantity);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quantity, MaxQuantity);

        return new Order(userId, productId, productName, unitPriceCents, quantity, ToMilliseconds(now));
    }

    public void MarkPaid(string transactionId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        EnsurePending();

        Status = OrderStatus.Paid;
        TransactionId = transactionId;
        UpdatedAt = ToMilliseconds(now);
    }

    public void MarkFailed(string failureReason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);
        EnsurePending();

        Status = OrderStatus.Failed;
        FailureReason = failureReason;
        UpdatedAt = ToMilliseconds(now);
    }

    /// <summary>
    /// An order's outcome is decided once. A Paid order must never become Failed (the customer
    /// was charged), and a Failed one must never become Paid.
    /// </summary>
    private void EnsurePending()
    {
        if (Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException($"Order {Id} is {Status}; only a Pending order can change status.");
        }
    }

    /// <summary>
    /// Timestamps are exposed with millisecond precision (contracts.md §1.3). Truncating here keeps
    /// what's stored, returned and compared identical.
    /// </summary>
    private static DateTimeOffset ToMilliseconds(DateTimeOffset value) =>
        new(value.UtcTicks - value.UtcTicks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
}
