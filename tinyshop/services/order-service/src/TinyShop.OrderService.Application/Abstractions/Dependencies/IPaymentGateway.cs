using TinyShop.OrderService.Application.Models;

namespace TinyShop.OrderService.Application.Abstractions.Dependencies;

/// <summary>
/// Charges payments through payment-service. Declared here, implemented in Infrastructure (over gRPC).
/// </summary>
/// <remarks>
/// Returns the business outcome (approved or declined). Technical failures are thrown, split the
/// way contracts.md §6 needs them, because they lead to different order states:
/// <list type="bullet">
///   <item><see cref="Common.Exceptions.PaymentTimeoutException"/>: no answer in time. The outcome
///   is unknown (the payment may have happened), so the order stays Pending.</item>
///   <item><see cref="Common.Exceptions.PaymentUnavailableException"/>: any other failure. Nothing
///   was charged, so the order becomes Failed.</item>
/// </list>
/// </remarks>
public interface IPaymentGateway
{
    Task<PaymentOutcome> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken);
}
