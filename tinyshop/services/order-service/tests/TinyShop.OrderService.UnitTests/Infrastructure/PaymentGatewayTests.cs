using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using TinyShop.OrderService.Application.Common.Exceptions;
using TinyShop.OrderService.Application.Models;
using TinyShop.OrderService.Infrastructure.Clients;
using V1 = TinyShop.Payment.V1;

namespace TinyShop.OrderService.UnitTests.Infrastructure;

/// <summary>The payment-service rows of contracts.md §6, at the gRPC level (§9.3).</summary>
public class PaymentGatewayTests
{
    private static readonly ChargeRequest Charge = new(1001, 123, 20_000);

    [Fact]
    public async Task Request_CarriesTheOrderUserAndAmount_WithoutADeadline()
    {
        V1.ProcessPaymentRequest? sent = null;
        CallOptions options = default;
        var gateway = Gateway((request, callOptions) =>
        {
            sent = request;
            options = callOptions;
            return Task.FromResult(new V1.ProcessPaymentResponse { Success = true, TransactionId = "txn_1" });
        });

        await gateway.ChargeAsync(Charge, TestContext.Current.CancellationToken);

        Assert.Equal(new V1.ProcessPaymentRequest { OrderId = 1001, UserId = 123, AmountCents = 20_000 }, sent);
        // M1 sets no deadline (contracts.md §7).
        Assert.Null(options.Deadline);
    }

    [Fact]
    public async Task Approved_ReturnsApproveWithTheTransactionId()
    {
        var gateway = Gateway(new V1.ProcessPaymentResponse { Success = true, TransactionId = "txn_8f14e45f" });

        var outcome = await gateway.ChargeAsync(Charge, TestContext.Current.CancellationToken);

        Assert.True(outcome.Approved);
        Assert.Equal("txn_8f14e45f", outcome.TransactionId);
    }

    [Fact]
    public async Task Declined_ReturnsDeclineWithTheReason()
    {
        var gateway = Gateway(new V1.ProcessPaymentResponse { Success = false, FailureReason = "insufficient_funds" });

        var outcome = await gateway.ChargeAsync(Charge, TestContext.Current.CancellationToken);

        Assert.False(outcome.Approved);
        Assert.Equal("insufficient_funds", outcome.DeclineReason);
    }

    [Fact]
    public async Task EmptyResponse_IsADeclineNeverAnApproval()
    {
        // Proto3: an unset bool reads as false and an unset string as "" (contracts.md §9.2).
        var gateway = Gateway(new V1.ProcessPaymentResponse());

        var outcome = await gateway.ChargeAsync(Charge, TestContext.Current.CancellationToken);

        Assert.False(outcome.Approved);
        Assert.Equal(PaymentGateway.UnspecifiedDeclineReason, outcome.DeclineReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ApprovedWithoutATransactionId_BreaksTheContract(string transactionId)
    {
        var gateway = Gateway(new V1.ProcessPaymentResponse { Success = true, TransactionId = transactionId });

        await Assert.ThrowsAsync<PaymentUnavailableException>(
            () => gateway.ChargeAsync(Charge, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task OverlongValues_BreakTheContract()
    {
        var approved = Gateway(new V1.ProcessPaymentResponse { Success = true, TransactionId = new string('t', 101) });
        var declined = Gateway(new V1.ProcessPaymentResponse { FailureReason = new string('r', 101) });

        await Assert.ThrowsAsync<PaymentUnavailableException>(
            () => approved.ChargeAsync(Charge, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<PaymentUnavailableException>(
            () => declined.ChargeAsync(Charge, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeadlineExceeded_IsPaymentTimeout()
    {
        var gateway = Gateway(StatusCode.DeadlineExceeded);

        var exception = await Assert.ThrowsAsync<PaymentTimeoutException>(
            () => gateway.ChargeAsync(Charge, TestContext.Current.CancellationToken));
        Assert.IsType<RpcException>(exception.InnerException);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable)]
    [InlineData(StatusCode.Internal)]
    [InlineData(StatusCode.InvalidArgument)]
    [InlineData(StatusCode.Unknown)]
    [InlineData(StatusCode.Unimplemented)]
    [InlineData(StatusCode.ResourceExhausted)]
    public async Task AnyOtherStatus_IsPaymentUnavailable(StatusCode status)
    {
        var gateway = Gateway(status);

        var exception = await Assert.ThrowsAsync<PaymentUnavailableException>(
            () => gateway.ChargeAsync(Charge, TestContext.Current.CancellationToken));
        Assert.Equal(status, Assert.IsType<RpcException>(exception.InnerException).StatusCode);
    }

    [Fact]
    public async Task CancelledByPaymentService_IsPaymentUnavailable()
    {
        // CANCELLED while our caller is still waiting: the server gave up, so it's a failure.
        var gateway = Gateway(StatusCode.Cancelled);

        await Assert.ThrowsAsync<PaymentUnavailableException>(
            () => gateway.ChargeAsync(Charge, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CancelledBecauseOurCallerLeft_IsACancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var gateway = Gateway(async (_, _) =>
        {
            await cancellation.CancelAsync();
            throw new RpcException(new Status(StatusCode.Cancelled, "Call canceled by the client."));
        });

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => gateway.ChargeAsync(Charge, cancellation.Token));
        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    private static PaymentGateway Gateway(V1.ProcessPaymentResponse response) =>
        Gateway((_, _) => Task.FromResult(response));

    private static PaymentGateway Gateway(StatusCode status) =>
        Gateway((_, _) => Task.FromException<V1.ProcessPaymentResponse>(new RpcException(new Status(status, "test"))));

    private static PaymentGateway Gateway(Func<V1.ProcessPaymentRequest, CallOptions, Task<V1.ProcessPaymentResponse>> handle) =>
        new(
            new V1.PaymentService.PaymentServiceClient(new FakePaymentCallInvoker(handle)),
            NullLogger<PaymentGateway>.Instance);
}
