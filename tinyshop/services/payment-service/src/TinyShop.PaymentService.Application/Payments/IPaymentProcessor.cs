using TinyShop.PaymentService.Domain.Payments;

namespace TinyShop.PaymentService.Application.Payments;

/// <summary>The payment use case, as called by the gRPC layer.</summary>
public interface IPaymentProcessor
{
    /// <summary>
    /// Processes a charge and returns its business outcome. Technical failures are thrown as
    /// exceptions, never returned as a declined result.
    /// </summary>
    Task<PaymentResult> ProcessAsync(Charge charge, CancellationToken cancellationToken);
}
