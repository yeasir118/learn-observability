namespace TinyShop.OrderService.Application.Models;

/// <summary>What order-service needs to know about a user: that it exists.</summary>
public sealed record UserSummary(long Id, string Name);

/// <summary>What order-service needs from a product: its name and current price, to snapshot.</summary>
public sealed record ProductSummary(long Id, string Name, long PriceCents);

/// <summary>A charge to request from payment-service.</summary>
public sealed record ChargeRequest(long OrderId, long UserId, long AmountCents);

/// <summary>The business outcome of a charge (contracts.md §9.3).</summary>
public sealed record PaymentOutcome
{
    private PaymentOutcome(bool approved, string? transactionId, string? declineReason)
    {
        Approved = approved;
        TransactionId = transactionId;
        DeclineReason = declineReason;
    }

    public bool Approved { get; }

    /// <summary>Set only when <see cref="Approved"/> is true.</summary>
    public string? TransactionId { get; }

    /// <summary>Set only when <see cref="Approved"/> is false.</summary>
    public string? DeclineReason { get; }

    public static PaymentOutcome Approve(string transactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        return new PaymentOutcome(true, transactionId, null);
    }

    public static PaymentOutcome Decline(string declineReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(declineReason);
        return new PaymentOutcome(false, null, declineReason);
    }
}
