using Microsoft.Extensions.Time.Testing;
using TinyShop.PaymentService.Application.Payments;
using TinyShop.PaymentService.Domain.Payments;
using TinyShop.ServiceDefaults.Abstractions.Faults;
using static TinyShop.PaymentService.UnitTests.Application.PaymentProcessorTests;

namespace TinyShop.PaymentService.UnitTests.Application;

/// <summary>
/// payment-service's fault switches (TS-016, decision 5). Time and randomness are both under the
/// test's control, so every result is exact: no waiting, no statistics.
/// </summary>
public class PaymentFaultTests
{
    private static readonly TimeSpan NormalDelay = TimeSpan.FromMilliseconds(50);
    private static readonly Charge User123 = new(orderId: 1001, userId: 123, amountCents: 20000);
    private static readonly Charge User124 = new(orderId: 1002, userId: 124, amountCents: 20000);

    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task Delay_IsAddedToTheNormalProcessingDelay()
    {
        var processor = CreateProcessor(_time, NormalDelay, new PaymentFaultOptions { Delay = TimeSpan.FromSeconds(3) });

        var processing = processor.ProcessAsync(User123, TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromSeconds(3) + NormalDelay - TimeSpan.FromMilliseconds(1));
        Assert.False(processing.IsCompleted, "Completed before the normal and the injected delay had passed.");

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True((await processing).Success);
    }

    [Theory]
    [InlineData(0.0, Outcome.Error)]
    [InlineData(0.099, Outcome.Error)]
    [InlineData(0.1, Outcome.Declined)]    // [0, 0.1) error, [0.1, 0.3) decline, the rest approved
    [InlineData(0.299, Outcome.Declined)]
    // Not exactly 0.3: in binary floating point 0.1 + 0.2 is 0.30000000000000004, so a draw of 0.3
    // still declines. The share is off by 1e-17, which no exercise can notice.
    [InlineData(0.301, Outcome.Approved)]
    [InlineData(0.999, Outcome.Approved)]
    public async Task Rates_SplitPaymentsExactlyByOneDraw(double draw, Outcome expected)
    {
        var processor = CreateProcessor(
            _time, TimeSpan.Zero, new PaymentFaultOptions { ErrorRate = 0.1, DeclineRate = 0.2 }, new FixedRandom(draw));

        Assert.Equal(expected, await OutcomeOf(processor, User123));
    }

    [Fact]
    public async Task Decline_IsANormalResult_WithTheCardDeclinedReason()
    {
        var processor = CreateProcessor(_time, TimeSpan.Zero, new PaymentFaultOptions { DeclineRate = 1 }, new FixedRandom(0.5));

        var result = await processor.ProcessAsync(User123, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Equal("card_declined", result.FailureReason);
        Assert.Null(result.TransactionId);
    }

    [Fact]
    public async Task Error_IsAnInjectedFaultException_NamingTheSettingAndTheOrder()
    {
        var processor = CreateProcessor(_time, TimeSpan.Zero, new PaymentFaultOptions { ErrorRate = 1 }, new FixedRandom(0.5));

        var exception = await Assert.ThrowsAsync<InjectedFaultException>(
            () => processor.ProcessAsync(User123, TestContext.Current.CancellationToken));

        Assert.Equal("Injected fault (Faults:ErrorRate=1): payment for order 1001 failed on purpose.", exception.Message);
    }

    [Fact]
    public async Task WithUserId_OnlyThatUsersPaymentsAreAffected()
    {
        var faults = new PaymentFaultOptions { ErrorRate = 1, Delay = TimeSpan.FromSeconds(3), UserId = 124 };
        var processor = CreateProcessor(_time, TimeSpan.Zero, faults, new FixedRandom(0.5));

        // User 123: no extra delay (completes at once) and approved.
        var unaffected = await processor.ProcessAsync(User123, TestContext.Current.CancellationToken);
        Assert.True(unaffected.Success);

        // User 124: delayed, then fails.
        var affected = processor.ProcessAsync(User124, TestContext.Current.CancellationToken);
        Assert.False(affected.IsCompleted);
        _time.Advance(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAsync<InjectedFaultException>(() => affected);
    }

    [Fact]
    public async Task AllOff_ApprovesWithoutDrawingARandomNumber()
    {
        var random = new FixedRandom(0.0);
        var processor = CreateProcessor(_time, TimeSpan.Zero, new PaymentFaultOptions(), random);

        Assert.True((await processor.ProcessAsync(User123, TestContext.Current.CancellationToken)).Success);
        Assert.Equal(0, random.Draws);
    }

    [Fact]
    public void Validate_RejectsRatesThatAddUpToMoreThanOne()
    {
        var options = new PaymentFaultOptions { DeclineRate = 0.7, ErrorRate = 0.5 };

        var error = Assert.Single(options.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(options)));

        Assert.Equal("Faults:DeclineRate + Faults:ErrorRate may not exceed 1.", error.ErrorMessage);
    }

    [Fact]
    public void DescribeActive_ListsEveryFaultThatIsOn_AndNothingWhenAllAreOff()
    {
        Assert.Empty(new PaymentFaultOptions().DescribeActive());
        Assert.Equal(
            [
                "Faults:Delay=00:00:03: every payment takes 3000 ms longer (user 123 only)",
                "Faults:DeclineRate=0.2: 20 % of payments are declined (user 123 only)",
            ],
            new PaymentFaultOptions { Delay = TimeSpan.FromSeconds(3), DeclineRate = 0.2, UserId = 123 }.DescribeActive());
    }

    public enum Outcome { Approved, Declined, Error }

    private static async Task<Outcome> OutcomeOf(PaymentProcessor processor, Charge charge)
    {
        try
        {
            var result = await processor.ProcessAsync(charge, TestContext.Current.CancellationToken);
            return result.Success ? Outcome.Approved : Outcome.Declined;
        }
        catch (InjectedFaultException)
        {
            return Outcome.Error;
        }
    }

    private sealed class FixedRandom(double value) : IFaultRandom
    {
        public int Draws { get; private set; }

        public double NextDouble()
        {
            Draws++;
            return value;
        }
    }
}
