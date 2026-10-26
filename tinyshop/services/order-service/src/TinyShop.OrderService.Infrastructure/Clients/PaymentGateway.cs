using Grpc.Core;
using Microsoft.Extensions.Logging;
using TinyShop.OrderService.Application.Abstractions.Dependencies;
using TinyShop.OrderService.Application.Common.Exceptions;
using TinyShop.OrderService.Application.Models;
using TinyShop.OrderService.Domain.Entities;
// Alias: "TinyShop.Payment" is a namespace, so short names from it read better qualified.
using V1 = TinyShop.Payment.V1;

namespace TinyShop.OrderService.Infrastructure.Clients;

/// <summary>
/// Charges payments through the generated gRPC client (contracts.md §9). The client comes from
/// the gRPC client factory, with its address set from configuration (DependencyInjection).
/// </summary>
/// <remarks>
/// Translation (contracts.md §9.3):
/// <code>
/// OK, success = true                 -> PaymentOutcome.Approve(transaction_id)
/// OK, success = false                -> PaymentOutcome.Decline(failure_reason)
/// DEADLINE_EXCEEDED                  -> PaymentTimeoutException   (order stays PENDING)
/// CANCELLED because our caller left  -> OperationCanceledException (nobody is waiting)
/// any other status (UNAVAILABLE,     -> PaymentUnavailableException (order FAILED)
///   INTERNAL, INVALID_ARGUMENT, ...)
/// OK but breaking the contract       -> PaymentUnavailableException
/// </code>
/// </remarks>
internal sealed class PaymentGateway(
    V1.PaymentService.PaymentServiceClient client,
    ILogger<PaymentGateway> logger) : IPaymentGateway
{
    /// <summary>Stored when a decline arrives without a reason: unset proto3 strings read as "".</summary>
    public const string UnspecifiedDeclineReason = "declined";

    public async Task<PaymentOutcome> ChargeAsync(ChargeRequest request, CancellationToken cancellationToken)
    {
        V1.ProcessPaymentResponse response;
        try
        {
            // No deadline: M1 sets no timeouts (contracts.md §7, decision D5), so this waits as long
            // as payment-service takes. Passing a deadline here is what would make DEADLINE_EXCEEDED possible.
            response = await client.ProcessPaymentAsync(
                new V1.ProcessPaymentRequest
                {
                    OrderId = request.OrderId,
                    UserId = request.UserId,
                    AmountCents = request.AmountCents,
                },
                cancellationToken: cancellationToken);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.DeadlineExceeded)
        {
            throw new PaymentTimeoutException("The payment call exceeded its deadline.", exception);
        }
        catch (RpcException exception) when (exception.StatusCode == StatusCode.Cancelled
            && cancellationToken.IsCancellationRequested)
        {
            // Our own caller cancelled (the HTTP client disconnected). Report it as the cancellation
            // it is, not as a payment failure.
            throw new OperationCanceledException("The payment call was cancelled by the caller.", exception, cancellationToken);
        }
        catch (RpcException exception)
        {
            // UNAVAILABLE (couldn't connect: the HttpRequestException is the inner exception),
            // INTERNAL, INVALID_ARGUMENT or anything else: no answer was produced, so nothing was charged.
            logger.LogWarning(exception, "Payment for order {OrderId} failed with gRPC status {StatusCode}",
                request.OrderId, exception.StatusCode);
            throw new PaymentUnavailableException($"payment-service failed with status {exception.StatusCode}.", exception);
        }

        return ToOutcome(request.OrderId, response);
    }

    private PaymentOutcome ToOutcome(long orderId, V1.ProcessPaymentResponse response)
    {
        if (response.Success)
        {
            // An approval must carry a transaction ID that fits the order (contracts.md §9.2).
            if (string.IsNullOrWhiteSpace(response.TransactionId)
                || response.TransactionId.Length > Order.TransactionIdMaxLength)
            {
                logger.LogError("payment-service approved order {OrderId} without a valid transaction ID", orderId);
                throw new PaymentUnavailableException("payment-service approved without a valid transaction ID.");
            }

            return PaymentOutcome.Approve(response.TransactionId);
        }

        var reason = string.IsNullOrWhiteSpace(response.FailureReason)
            ? UnspecifiedDeclineReason
            : response.FailureReason;

        if (reason.Length > Order.FailureReasonMaxLength)
        {
            logger.LogError("payment-service declined order {OrderId} with an over-long reason", orderId);
            throw new PaymentUnavailableException("payment-service declined with an invalid reason.");
        }

        return PaymentOutcome.Decline(reason);
    }
}
