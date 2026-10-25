namespace TinyShop.PaymentService.Domain.Payments;

/// <summary>
/// A charge: an amount billed to a user for an order. Amounts are whole cents (contracts.md §1.2).
/// Invalid values are rejected here, so an invalid charge can't exist in memory.
/// </summary>
/// <remarks>
/// Not called "Payment": the generated gRPC code lives in TinyShop.Payment.V1, which creates a
/// TinyShop.Payment namespace, and inside any TinyShop.* namespace the name "Payment" would resolve
/// to that namespace instead of this class (error CS0118).
/// </remarks>
public sealed class Charge
{
    public Charge(long orderId, long userId, long amountCents)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(orderId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountCents);

        OrderId = orderId;
        UserId = userId;
        AmountCents = amountCents;
    }

    public long OrderId { get; }

    public long UserId { get; }

    public long AmountCents { get; }
}
