using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyShop.PaymentService.Domain.Payments;
using TinyShop.ServiceDefaults.Abstractions.Faults;

namespace TinyShop.PaymentService.Application.Payments;

/// <summary>
/// Simulated payment processing: waits <see cref="PaymentProcessingOptions.ProcessingDelay"/>,
/// then approves (outline §7). The fault switches (TS-016, <see cref="PaymentFaultOptions"/>) can
/// make it slower, decline, or fail.
/// </summary>
internal sealed class PaymentProcessor(
    TimeProvider timeProvider,
    IOptions<PaymentProcessingOptions> options,
    IOptions<PaymentFaultOptions> faultOptions,
    IFaultRandom random,
    ILogger<PaymentProcessor> logger) : IPaymentProcessor
{
    /// <summary>The reason an injected decline gives, as a real provider's decline would.</summary>
    public const string InjectedDeclineReason = "card_declined";

    public async Task<PaymentResult> ProcessAsync(Charge charge, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Processing payment for order {OrderId}, user {UserId}, {AmountCents} cents",
            charge.OrderId, charge.UserId, charge.AmountCents);

        var faults = faultOptions.Value;
        var faultsApply = faults.AppliesTo(charge.UserId);

        // Stands in for the round trip to a real payment provider. The delay goes through
        // TimeProvider, so tests can move time forward instead of actually waiting. An injected
        // delay adds to it: the provider is slow, and whatever it answers comes after the wait.
        var delay = options.Value.ProcessingDelay + (faultsApply ? faults.Delay : TimeSpan.Zero);
        await Task.Delay(delay, timeProvider, cancellationToken);

        if (faultsApply && faults.ErrorRate + faults.DeclineRate > 0)
        {
            // One draw for both: [0, ErrorRate) fails, [ErrorRate, ErrorRate + DeclineRate) declines,
            // the rest is approved. No draw at all while both are off (decision 10).
            var draw = random.NextDouble();
            if (draw < faults.ErrorRate)
            {
                throw new InjectedFaultException(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Injected fault (Faults:ErrorRate={faults.ErrorRate}): payment for order {charge.OrderId} failed on purpose."));
            }

            if (draw < faults.ErrorRate + faults.DeclineRate)
            {
                logger.LogInformation(
                    "Payment for order {OrderId} declined: {FailureReason}", charge.OrderId, InjectedDeclineReason);
                return PaymentResult.Declined(InjectedDeclineReason);
            }
        }

        var result = PaymentResult.Approved(TransactionIds.New());

        logger.LogInformation(
            "Payment for order {OrderId} approved: {TransactionId}", charge.OrderId, result.TransactionId);

        return result;
    }
}
