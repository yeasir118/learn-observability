using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TinyShop.PaymentService.Application.Payments;
using TinyShop.PaymentService.Domain.Payments;
using TinyShop.ServiceDefaults.Abstractions.Faults;

namespace TinyShop.PaymentService.UnitTests.Application;

public partial class PaymentProcessorTests
{
    private static readonly TimeSpan Delay = TimeSpan.FromMilliseconds(50);
    private static readonly Charge AnyCharge = new(orderId: 1001, userId: 123, amountCents: 20000);

    // A clock the test controls: time only moves when the test calls Advance(), so the
    // "waits 50 ms" behaviour is tested exactly, without the test itself waiting.
    private readonly FakeTimeProvider _time = new();
    private readonly PaymentProcessor _processor;

    public PaymentProcessorTests()
    {
        _processor = CreateProcessor(_time, Delay);
    }

    /// <summary>
    /// A processor with the given normal delay, and fault switches as configured (all off unless
    /// passed, as in production). <paramref name="random"/> decides the rate-based faults.
    /// </summary>
    internal static PaymentProcessor CreateProcessor(
        TimeProvider time,
        TimeSpan processingDelay,
        PaymentFaultOptions? faults = null,
        IFaultRandom? random = null) =>
        new(
            time,
            Options.Create(new PaymentProcessingOptions { ProcessingDelay = processingDelay }),
            Options.Create(faults ?? new PaymentFaultOptions()),
            random ?? new SharedFaultRandom(),
            NullLogger<PaymentProcessor>.Instance);

    [Fact]
    public async Task ProcessAsync_WaitsForTheProcessingDelay_ThenApproves()
    {
        var processing = _processor.ProcessAsync(AnyCharge, TestContext.Current.CancellationToken);

        _time.Advance(Delay - TimeSpan.FromMilliseconds(1));
        Assert.False(processing.IsCompleted, "Completed before the processing delay had passed.");

        _time.Advance(TimeSpan.FromMilliseconds(1));
        var result = await processing;

        Assert.True(result.Success);
        Assert.Null(result.FailureReason);
        Assert.Matches(TransactionIdFormat(), result.TransactionId);
    }

    [Fact]
    public async Task ProcessAsync_GivesEveryPaymentADifferentTransactionId()
    {
        var ids = new HashSet<string>();
        for (var i = 0; i < 100; i++)
        {
            var processing = _processor.ProcessAsync(AnyCharge, TestContext.Current.CancellationToken);
            _time.Advance(Delay);
            ids.Add((await processing).TransactionId!);
        }

        Assert.Equal(100, ids.Count);
    }

    [Fact]
    public async Task ProcessAsync_WhenCallerCancels_StopsWaiting()
    {
        using var cts = new CancellationTokenSource();
        var processing = _processor.ProcessAsync(AnyCharge, cts.Token);

        cts.Cancel();

        // The delay is abandoned; no result, and no payment is approved after the caller gave up.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => processing);
    }

    [Fact]
    public async Task ProcessAsync_WithZeroDelay_ApprovesImmediately()
    {
        var processor = CreateProcessor(_time, TimeSpan.Zero);

        var result = await processor.ProcessAsync(AnyCharge, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
    }

    [Fact]
    public void TransactionIds_HaveTheDocumentedFormat()
    {
        Assert.Matches(TransactionIdFormat(), TransactionIds.New());
    }

    [GeneratedRegex("^txn_[0-9a-f]{32}$")]
    private static partial Regex TransactionIdFormat();
}
