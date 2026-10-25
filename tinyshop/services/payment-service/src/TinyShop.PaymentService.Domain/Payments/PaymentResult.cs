namespace TinyShop.PaymentService.Domain.Payments;

/// <summary>
/// The business outcome of a payment: approved (with a transaction ID) or declined (with a reason).
/// A technical failure is not a result; it's an exception, which becomes a gRPC error status
/// (contracts.md §9.3).
/// </summary>
public sealed record PaymentResult
{
    private PaymentResult(bool success, string? transactionId, string? failureReason)
    {
        Success = success;
        TransactionId = transactionId;
        FailureReason = failureReason;
    }

    public bool Success { get; }

    /// <summary>Set only when <see cref="Success"/> is true.</summary>
    public string? TransactionId { get; }

    /// <summary>Set only when <see cref="Success"/> is false. A snake_case reason code.</summary>
    public string? FailureReason { get; }

    // The only two ways to create a result, so a "successful payment without a transaction ID"
    // or a "decline without a reason" can't be built.
    public static PaymentResult Approved(string transactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        return new PaymentResult(true, transactionId, null);
    }

    public static PaymentResult Declined(string failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);
        return new PaymentResult(false, null, failureReason);
    }
}
