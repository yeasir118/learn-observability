using System.Net;
using Grpc.Core;
using Grpc.Health.V1;
using Microsoft.Extensions.Options;
using TinyShop.PaymentService.IntegrationTests.Infrastructure;
using V1 = TinyShop.Payment.V1;

namespace TinyShop.PaymentService.IntegrationTests.Faults;

/// <summary>
/// The fault switches through the whole service, over real gRPC (TS-016, AC5-AC7). Rates of 0 and 1
/// only, so every result is certain; the unit tests cover rates in between exactly.
/// </summary>
public class PaymentFaultTests
{
    private static V1.ProcessPaymentRequest Request(long userId) => new()
    {
        OrderId = 1001,
        UserId = userId,
        AmountCents = 20000,
    };

    private static PaymentServiceFactory WithFaults(params (string Key, string? Value)[] settings) =>
        new(settings.ToDictionary(s => s.Key, s => s.Value));

    [Fact]
    public async Task ErrorRateOne_FailsWithInternal_WithoutDetails()
    {
        await using var factory = WithFaults(("Faults:ErrorRate", "1"));

        var exception = await Assert.ThrowsAsync<RpcException>(async () =>
            await factory.CreatePaymentClient().ProcessPaymentAsync(
                Request(userId: 123), cancellationToken: TestContext.Current.CancellationToken));

        // The interceptor's generic answer: order-service maps it to 502 payment_unavailable.
        Assert.Equal(StatusCode.Internal, exception.StatusCode);
        Assert.Equal("Payment processing failed.", exception.Status.Detail);
    }

    [Fact]
    public async Task DeclineRateOne_DeclinesWithCardDeclined()
    {
        await using var factory = WithFaults(("Faults:DeclineRate", "1"));

        var response = await factory.CreatePaymentClient().ProcessPaymentAsync(
            Request(userId: 123), cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(response.Success);
        Assert.Equal("card_declined", response.FailureReason);
        Assert.Equal(string.Empty, response.TransactionId);
    }

    [Theory]
    [InlineData(123, true)]
    [InlineData(124, false)]
    public async Task WithUserId_OnlyThatUsersPaymentsFail(long userId, bool approved)
    {
        await using var factory = WithFaults(("Faults:DeclineRate", "1"), ("Faults:UserId", "124"));

        var response = await factory.CreatePaymentClient().ProcessPaymentAsync(
            Request(userId), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(approved, response.Success);
    }

    [Fact]
    public async Task AllOff_AsComposePassesThem_Approves()
    {
        // What Compose passes when no variable is set in .env: zeros, and an empty user ID.
        await using var factory = WithFaults(
            ("Faults:Delay", "00:00:00"), ("Faults:DeclineRate", "0"), ("Faults:ErrorRate", "0"), ("Faults:UserId", ""));

        var response = await factory.CreatePaymentClient().ProcessPaymentAsync(
            Request(userId: 123), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(response.Success);
    }

    [Fact]
    public async Task ErrorRateOne_NeverAffectsHealthChecks()
    {
        await using var factory = WithFaults(("Faults:ErrorRate", "1"), ("Faults:Delay", "00:00:30"));

        var grpcHealth = await new global::Grpc.Health.V1.Health.HealthClient(factory.CreateChannel()).CheckAsync(
            new HealthCheckRequest(), cancellationToken: TestContext.Current.CancellationToken);
        var ready = await factory.CreateClient().GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HealthCheckResponse.Types.ServingStatus.Serving, grpcHealth.Status);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Theory]
    [InlineData("Faults:ErrorRate", "2", "ErrorRate")]
    [InlineData("Faults:DeclineRate", "-1", "DeclineRate")]
    [InlineData("Faults:Delay", "00:02:00", "Delay")]
    [InlineData("Faults:UserId", "0", "UserId")]
    public async Task InvalidSetting_StopsTheServiceAtStartup_NamingTheSetting(string key, string value, string setting)
    {
        await using var factory = WithFaults((key, value));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains(setting, exception.Message);
    }

    [Fact]
    public async Task RatesAddingUpToMoreThanOne_StopTheServiceAtStartup()
    {
        await using var factory = WithFaults(("Faults:DeclineRate", "0.7"), ("Faults:ErrorRate", "0.5"));

        var exception = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("may not exceed 1", exception.Message);
    }

    [Fact]
    public async Task UnparsableDelay_StopsTheServiceAtStartup_NamingTheSetting()
    {
        await using var factory = WithFaults(("Faults:Delay", "abc"));

        var exception = Assert.ThrowsAny<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Delay", exception.Message);
    }
}
