using Grpc.Core;
using TinyShop.PaymentService.Application.Payments;
using TinyShop.PaymentService.Domain.Payments;
// The generated code lives in TinyShop.Payment.V1 (the proto's csharp_namespace). Inside this
// project's TinyShop.PaymentService namespace, the bare name "PaymentService" would resolve to
// that namespace instead of the generated class (error CS0118), hence the alias.
using V1 = TinyShop.Payment.V1;

namespace TinyShop.PaymentService.Api.Services;

/// <summary>
/// Implements tinyshop.payment.v1.PaymentService (contracts.md §9). Translates between protobuf
/// messages and the application's types; contains no payment logic itself.
/// </summary>
internal sealed class PaymentGrpcService(IPaymentProcessor processor) : V1.PaymentService.PaymentServiceBase
{
    public override async Task<V1.ProcessPaymentResponse> ProcessPayment(
        V1.ProcessPaymentRequest request,
        ServerCallContext context)
    {
        var charge = ToCharge(request);

        // ServerCallContext.CancellationToken fires when the caller cancels or its deadline passes.
        var result = await processor.ProcessAsync(charge, context.CancellationToken);

        return new V1.ProcessPaymentResponse
        {
            Success = result.Success,
            // Proto3 strings can't be null; an unset string is "" (contracts.md §9.2).
            TransactionId = result.TransactionId ?? string.Empty,
            FailureReason = result.FailureReason ?? string.Empty,
        };
    }

    /// <summary>
    /// Proto3 can't mark fields as required: a missing field simply reads as 0. So every field is
    /// checked here, and all problems are reported at once with gRPC's INVALID_ARGUMENT status.
    /// </summary>
    private static Charge ToCharge(V1.ProcessPaymentRequest request)
    {
        var problems = new List<string>();
        if (request.OrderId <= 0)
        {
            problems.Add("order_id must be greater than 0");
        }

        if (request.UserId <= 0)
        {
            problems.Add("user_id must be greater than 0");
        }

        if (request.AmountCents <= 0)
        {
            problems.Add("amount_cents must be greater than 0");
        }

        if (problems.Count > 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, string.Join("; ", problems)));
        }

        return new Charge(request.OrderId, request.UserId, request.AmountCents);
    }
}
