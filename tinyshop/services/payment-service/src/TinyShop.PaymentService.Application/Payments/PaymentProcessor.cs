using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyShop.PaymentService.Domain.Payments;

namespace TinyShop.PaymentService.Application.Payments;

/// <summary>
/// Simulated payment processing: waits <see cref="PaymentProcessingOptions.ProcessingDelay"/>,
/// then approves (outline §7). Declines and failures arrive with the fault switches (TS-016).
/// </summary>
internal sealed class PaymentProcessor(
    TimeProvider timeProvider,
    IOptions<PaymentProcessingOptions> options,
    ILogger<PaymentProcessor> logger) : IPaymentProcessor
{
    public async Task<PaymentResult> ProcessAsync(Charge charge, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Processing payment for order {OrderId}, user {UserId}, {AmountCents} cents",
            charge.OrderId, charge.UserId, charge.AmountCents);

        // Stands in for the round trip to a real payment provider. The delay goes through
        // TimeProvider, so tests can move time forward instead of actually waiting.
        await Task.Delay(options.Value.ProcessingDelay, timeProvider, cancellationToken);

        var result = PaymentResult.Approved(TransactionIds.New());

        logger.LogInformation(
            "Payment for order {OrderId} approved: {TransactionId}", charge.OrderId, result.TransactionId);

        return result;
    }
}
