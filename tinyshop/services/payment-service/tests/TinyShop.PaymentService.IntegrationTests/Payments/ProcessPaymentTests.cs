using System.Text.RegularExpressions;
using Grpc.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using TinyShop.PaymentService.Application.Payments;
using TinyShop.PaymentService.Domain.Payments;
using TinyShop.PaymentService.IntegrationTests.Infrastructure;
using V1 = TinyShop.Payment.V1;

namespace TinyShop.PaymentService.IntegrationTests.Payments;

/// <summary>PaymentService.ProcessPayment over real gRPC (contracts.md §9).</summary>
public partial class ProcessPaymentTests(PaymentServiceFactory factory)
{
    private readonly V1.PaymentService.PaymentServiceClient _client = factory.CreatePaymentClient();

    private static V1.ProcessPaymentRequest ValidRequest() => new()
    {
        OrderId = 1001,
        UserId = 123,
        AmountCents = 20000,
    };

    [Fact]
    public async Task ProcessPayment_WithValidRequest_Approves()
    {
        var response = await _client.ProcessPaymentAsync(
            ValidRequest(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Success);
        Assert.Matches(TransactionIdFormat(), response.TransactionId);
        // Proto3 strings have no null: "not applicable" is the empty string (contracts.md §9.2).
        Assert.Equal(string.Empty, response.FailureReason);
    }

    [Fact]
    public async Task ProcessPayment_TwoPayments_GetDifferentTransactionIds()
    {
        var first = await _client.ProcessPaymentAsync(ValidRequest(), cancellationToken: TestContext.Current.CancellationToken);
        var second = await _client.ProcessPaymentAsync(ValidRequest(), cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(first.TransactionId, second.TransactionId);
    }

    [Theory]
    [InlineData(0, 123, 100, "order_id")]
    [InlineData(-1, 123, 100, "order_id")]
    [InlineData(1001, 0, 100, "user_id")]
    [InlineData(1001, 123, 0, "amount_cents")]
    [InlineData(1001, 123, -100, "amount_cents")]
    public async Task ProcessPayment_WithInvalidField_ReturnsInvalidArgument(
        long orderId, long userId, long amountCents, string field)
    {
        var request = new V1.ProcessPaymentRequest { OrderId = orderId, UserId = userId, AmountCents = amountCents };

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await _client.ProcessPaymentAsync(request, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Contains(field, exception.Status.Detail);
    }

    [Fact]
    public async Task ProcessPayment_WithEmptyRequest_ReportsEveryMissingField()
    {
        // Proto3 has no "required": an empty message arrives with every field set to 0.
        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await _client.ProcessPaymentAsync(new V1.ProcessPaymentRequest(), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(StatusCode.InvalidArgument, exception.StatusCode);
        Assert.Contains("order_id", exception.Status.Detail);
        Assert.Contains("user_id", exception.Status.Detail);
        Assert.Contains("amount_cents", exception.Status.Detail);
    }

    [Fact]
    public async Task ProcessPayment_WhenDeadlinePasses_ReturnsDeadlineExceeded()
    {
        // A slow payment (2 s) and a caller that waits at most 200 ms: the deadline travels with
        // the call (as the grpc-timeout header), and the client gets DEADLINE_EXCEEDED.
        // This is the outcome contracts.md §6 maps to 504 payment_timeout.
        await using var slow = new PaymentServiceFactory(new Dictionary<string, string?>
        {
            ["Payment:ProcessingDelay"] = "00:00:02",
        });
        var client = slow.CreatePaymentClient();

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.ProcessPaymentAsync(
                ValidRequest(),
                deadline: DateTime.UtcNow.AddMilliseconds(200),
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(StatusCode.DeadlineExceeded, exception.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WhenProcessingThrows_ReturnsInternalWithoutDetails()
    {
        // Replace the processor with one that fails, to exercise the exception interceptor.
        await using var failing = new PaymentServiceFactory();
        var client = new V1.PaymentService.PaymentServiceClient(
            failing.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                    services.AddScoped<IPaymentProcessor, ThrowingProcessor>()))
                .CreateGrpcChannel());

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await client.ProcessPaymentAsync(ValidRequest(), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(StatusCode.Internal, exception.StatusCode);
        Assert.Equal("Payment processing failed.", exception.Status.Detail);
        Assert.DoesNotContain("secret", exception.Status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex("^txn_[0-9a-f]{32}$")]
    private static partial Regex TransactionIdFormat();

    private sealed class ThrowingProcessor : IPaymentProcessor
    {
        public Task<PaymentResult> ProcessAsync(Charge charge, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("secret internal detail: provider connection string ...");
    }
}
