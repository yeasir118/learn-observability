using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TinyShop.OrderService.Application.Abstractions.Dependencies;
using TinyShop.OrderService.Application.Abstractions.Persistence;
using TinyShop.OrderService.Application.Common.Exceptions;
using TinyShop.OrderService.Application.Models;
using TinyShop.OrderService.Application.Services;
using TinyShop.OrderService.Domain.Entities;

namespace TinyShop.OrderService.UnitTests.Application;

/// <summary>
/// The order flow against substitutes for every dependency: each row of contracts.md §6, as the
/// use case sees it (the adapters' translation of HTTP and gRPC is tested separately).
/// </summary>
public class OrderPlacementTests
{
    private const long OrderId = 1001;
    private static readonly PlaceOrderCommand Command = new(123, 1, 2);

    private readonly IUserDirectory _users = Substitute.For<IUserDirectory>();
    private readonly IProductCatalog _products = Substitute.For<IProductCatalog>();
    private readonly IPaymentGateway _payments = Substitute.For<IPaymentGateway>();
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 18, 14, 30, 5, 123, TimeSpan.Zero));
    private readonly OrderPlacement _placement;

    /// <summary>Every order the use case handed to the repository.</summary>
    private readonly List<Order> _added = [];

    /// <summary>The status of the order each time SaveChangesAsync was called.</summary>
    private readonly List<string> _savedStatuses = [];

    public OrderPlacementTests()
    {
        _users.FindAsync(123, Arg.Any<CancellationToken>()).Returns(new UserSummary(123, "Alice"));
        _products.FindAsync(1, Arg.Any<CancellationToken>()).Returns(new ProductSummary(1, "Keyboard", 10_000));

        _orders.When(repository => repository.Add(Arg.Any<Order>())).Do(call => _added.Add(call.Arg<Order>()));
        _orders.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            var order = _added.Single();
            // The database assigns the ID on the first save, as EF Core does.
            if (order.Id == 0)
            {
                SetId(order, OrderId);
            }

            _savedStatuses.Add(order.Status.ToString());
            return Task.CompletedTask;
        });

        _placement = new OrderPlacement(
            _users, _products, _payments, _orders, _time, NullLogger<OrderPlacement>.Instance);
    }

    [Fact]
    public async Task PaymentApproved_SavesPendingThenPaid_AndReturnsPlaced()
    {
        _payments.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                _time.Advance(TimeSpan.FromMilliseconds(64));
                return PaymentOutcome.Approve("txn_1");
            });

        var result = await _placement.PlaceAsync(Command, TestContext.Current.CancellationToken);

        var order = Assert.IsType<PlaceOrderResult.Placed>(result).Order;
        Assert.Equal(OrderId, order.Id);
        Assert.Equal("Keyboard", order.ProductName);
        Assert.Equal(10_000, order.UnitPriceCents);
        Assert.Equal(20_000, order.TotalCents);
        Assert.Equal("PAID", order.Status);
        Assert.Equal("txn_1", order.TransactionId);
        Assert.Null(order.FailureReason);
        Assert.Equal(order.CreatedAt.AddMilliseconds(64), order.UpdatedAt);
        Assert.Equal(["Pending", "Paid"], _savedStatuses);
    }

    [Fact]
    public async Task Payment_IsChargedTheTotal_ForTheSavedOrder()
    {
        _payments.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(PaymentOutcome.Approve("txn_1"));

        await _placement.PlaceAsync(Command, TestContext.Current.CancellationToken);

        // The order is saved (and has its ID) before the charge, which is made with that ID.
        await _payments.Received(1).ChargeAsync(new ChargeRequest(OrderId, 123, 20_000), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PaymentDeclined_SavesFailedWithTheDeclineReason_AndReturnsPlaced()
    {
        _payments.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(PaymentOutcome.Decline("insufficient_funds"));

        var result = await _placement.PlaceAsync(Command, TestContext.Current.CancellationToken);

        var order = Assert.IsType<PlaceOrderResult.Placed>(result).Order;
        Assert.Equal("FAILED", order.Status);
        Assert.Equal("insufficient_funds", order.FailureReason);
        Assert.Null(order.TransactionId);
        Assert.Equal(["Pending", "Failed"], _savedStatuses);
    }

    [Fact]
    public async Task PaymentUnavailable_SavesFailedWithPaymentUnavailable()
    {
        _payments.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PaymentUnavailableException("down"));

        var result = await _placement.PlaceAsync(Command, TestContext.Current.CancellationToken);

        var order = Assert.IsType<PlaceOrderResult.PaymentUnavailable>(result).Order;
        Assert.Equal(OrderId, order.Id);
        Assert.Equal("FAILED", order.Status);
        Assert.Equal("payment_unavailable", order.FailureReason);
        Assert.Equal(["Pending", "Failed"], _savedStatuses);
    }

    [Fact]
    public async Task PaymentTimeout_LeavesTheOrderPending()
    {
        _payments.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PaymentTimeoutException("slow"));

        var result = await _placement.PlaceAsync(Command, TestContext.Current.CancellationToken);

        var order = Assert.IsType<PlaceOrderResult.PaymentTimedOut>(result).Order;
        Assert.Equal(OrderId, order.Id);
        Assert.Equal("PENDING", order.Status);
        // Saved once (the insert), never updated.
        Assert.Equal(["Pending"], _savedStatuses);
    }

    [Fact]
    public async Task UnknownUser_ReturnsUserNotFound_WithoutCallingAnythingElse()
    {
        _users.FindAsync(123, Arg.Any<CancellationToken>()).Returns((UserSummary?)null);

        var result = await _placement.PlaceAsync(Command, TestContext.Current.CancellationToken);

        Assert.Equal(new PlaceOrderResult.UserNotFound(123), result);
        await AssertNothingAfterUserServiceAsync();
    }

    [Fact]
    public async Task UnknownProduct_ReturnsProductNotFound_AndCreatesNoOrder()
    {
        _products.FindAsync(1, Arg.Any<CancellationToken>()).Returns((ProductSummary?)null);

        var result = await _placement.PlaceAsync(Command, TestContext.Current.CancellationToken);

        Assert.Equal(new PlaceOrderResult.ProductNotFound(1), result);
        AssertNoOrderCreated();
        await _payments.DidNotReceive().ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>());
    }

    public static TheoryData<Exception> DependencyFailures =>
    [
        new DependencyUnavailableException("user-service", "5xx or couldn't connect"),
        new DependencyTimeoutException("user-service", "timeout"),
        new InvalidOperationException("other 4xx: our bug"),
    ];

    [Theory]
    [MemberData(nameof(DependencyFailures))]
    public async Task UserServiceFailure_Propagates_AndCreatesNoOrder(Exception failure)
    {
        _users.FindAsync(123, Arg.Any<CancellationToken>()).ThrowsAsync(failure);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(
            () => _placement.PlaceAsync(Command, TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        await AssertNothingAfterUserServiceAsync();
    }

    [Theory]
    [MemberData(nameof(DependencyFailures))]
    public async Task ProductServiceFailure_Propagates_AndCreatesNoOrder(Exception failure)
    {
        _products.FindAsync(1, Arg.Any<CancellationToken>()).ThrowsAsync(failure);

        var thrown = await Assert.ThrowsAnyAsync<Exception>(
            () => _placement.PlaceAsync(Command, TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        AssertNoOrderCreated();
        await _payments.DidNotReceive().ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InsertFails_Propagates_AndNothingIsCharged()
    {
        _orders.SaveChangesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new DataStoreUnavailableException("order-db down", new TimeoutException()));

        await Assert.ThrowsAsync<DataStoreUnavailableException>(
            () => _placement.PlaceAsync(Command, TestContext.Current.CancellationToken));

        await _payments.DidNotReceive().ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StatusUpdateFails_AfterPayment_ReturnsStatusNotRecordedWithTheOrderId(bool approved)
    {
        _payments.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .Returns(approved ? PaymentOutcome.Approve("txn_1") : PaymentOutcome.Decline("declined"));
        FailSavesAfterTheInsert();

        var result = await _placement.PlaceAsync(Command, TestContext.Current.CancellationToken);

        Assert.Equal(new PlaceOrderResult.StatusNotRecorded(OrderId), result);
    }

    [Fact]
    public async Task StatusUpdateFails_AfterPaymentUnavailable_ReturnsStatusNotRecorded()
    {
        _payments.ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new PaymentUnavailableException("down"));
        FailSavesAfterTheInsert();

        var result = await _placement.PlaceAsync(Command, TestContext.Current.CancellationToken);

        Assert.Equal(new PlaceOrderResult.StatusNotRecorded(OrderId), result);
    }

    /// <summary>The first save (the insert) succeeds and assigns the ID; every later one fails.</summary>
    private void FailSavesAfterTheInsert()
    {
        var saves = 0;
        _orders.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (saves++ == 0)
            {
                SetId(_added.Single(), OrderId);
                return Task.CompletedTask;
            }

            return Task.FromException(new DataStoreUnavailableException("order-db down", new TimeoutException()));
        });
    }

    private async Task AssertNothingAfterUserServiceAsync()
    {
        await _products.DidNotReceive().FindAsync(Arg.Any<long>(), Arg.Any<CancellationToken>());
        AssertNoOrderCreated();
        await _payments.DidNotReceive().ChargeAsync(Arg.Any<ChargeRequest>(), Arg.Any<CancellationToken>());
    }

    private void AssertNoOrderCreated()
    {
        Assert.Empty(_added);
        Assert.Empty(_savedStatuses);
    }

    /// <summary>Plays the database's part: Id has a private setter, assigned by EF Core in the real app.</summary>
    private static void SetId(Order order, long id) =>
        typeof(Order).GetProperty(nameof(Order.Id))!.SetValue(order, id, BindingFlags.NonPublic | BindingFlags.Instance, null, null, null);
}
