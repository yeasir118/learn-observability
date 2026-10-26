using TinyShop.OrderService.Domain.Entities;
using TinyShop.OrderService.Domain.Enums;

namespace TinyShop.OrderService.UnitTests.Domain;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 14, 30, 5, 123, TimeSpan.Zero);

    [Fact]
    public void Place_CreatesAPendingOrderWithASnapshotAndTotal()
    {
        var order = Order.Place(123, 1, "Keyboard", 10_000, 2, Now);

        Assert.Equal(123, order.UserId);
        Assert.Equal(1, order.ProductId);
        Assert.Equal("Keyboard", order.ProductName);
        Assert.Equal(10_000, order.UnitPriceCents);
        Assert.Equal(2, order.Quantity);
        Assert.Equal(20_000, order.TotalCents);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Null(order.FailureReason);
        Assert.Null(order.TransactionId);
        Assert.Equal(Now, order.CreatedAt);
        Assert.Equal(Now, order.UpdatedAt);
    }

    [Fact]
    public void Place_TruncatesTimestampsToMillisecondsInUtc()
    {
        // 14:30:05.1234567 at +02:00
        var local = new DateTimeOffset(2026, 10, 18, 16, 30, 5, TimeSpan.FromHours(2)).AddTicks(1_234_567);

        var order = Order.Place(123, 1, "Keyboard", 10_000, 1, local);

        Assert.Equal(new DateTimeOffset(2026, 10, 18, 14, 30, 5, 123, TimeSpan.Zero), order.CreatedAt);
        Assert.Equal(TimeSpan.Zero, order.CreatedAt.Offset);
    }

    [Theory]
    [InlineData(0, 1, "Keyboard", 10_000, 1)]
    [InlineData(123, 0, "Keyboard", 10_000, 1)]
    [InlineData(123, 1, "", 10_000, 1)]
    [InlineData(123, 1, "   ", 10_000, 1)]
    [InlineData(123, 1, "Keyboard", 0, 1)]
    [InlineData(123, 1, "Keyboard", 10_000, 0)]
    [InlineData(123, 1, "Keyboard", 10_000, 101)]
    public void Place_RejectsInvalidValues(long userId, long productId, string productName, long priceCents, int quantity)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Order.Place(userId, productId, productName, priceCents, quantity, Now));
    }

    [Fact]
    public void Place_RejectsAnOverlongProductName()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Order.Place(123, 1, new string('x', Order.ProductNameMaxLength + 1), 10_000, 1, Now));
    }

    [Fact]
    public void Place_ThrowsInsteadOfOverflowingTheTotal()
    {
        Assert.Throws<OverflowException>(() => Order.Place(123, 1, "Yacht", long.MaxValue / 2, 3, Now));
    }

    [Fact]
    public void MarkPaid_SetsTheTransactionAndUpdatedAt()
    {
        var order = Order.Place(123, 1, "Keyboard", 10_000, 1, Now);

        order.MarkPaid("txn_1", Now.AddMilliseconds(64));

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal("txn_1", order.TransactionId);
        Assert.Null(order.FailureReason);
        Assert.Equal(Now, order.CreatedAt);
        Assert.Equal(Now.AddMilliseconds(64), order.UpdatedAt);
    }

    [Fact]
    public void MarkFailed_SetsTheReasonAndUpdatedAt()
    {
        var order = Order.Place(123, 1, "Keyboard", 10_000, 1, Now);

        order.MarkFailed("insufficient_funds", Now.AddMilliseconds(64));

        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Equal("insufficient_funds", order.FailureReason);
        Assert.Null(order.TransactionId);
        Assert.Equal(Now.AddMilliseconds(64), order.UpdatedAt);
    }

    [Fact]
    public void AnOrdersOutcome_IsDecidedOnlyOnce()
    {
        var paid = Order.Place(123, 1, "Keyboard", 10_000, 1, Now);
        paid.MarkPaid("txn_1", Now);
        var failed = Order.Place(123, 1, "Keyboard", 10_000, 1, Now);
        failed.MarkFailed("declined", Now);

        Assert.Throws<InvalidOperationException>(() => paid.MarkFailed("declined", Now));
        Assert.Throws<InvalidOperationException>(() => paid.MarkPaid("txn_2", Now));
        Assert.Throws<InvalidOperationException>(() => failed.MarkPaid("txn_1", Now));
    }

    [Fact]
    public void MarkPaidAndMarkFailed_RequireAValue()
    {
        var order = Order.Place(123, 1, "Keyboard", 10_000, 1, Now);

        Assert.ThrowsAny<ArgumentException>(() => order.MarkPaid("", Now));
        Assert.ThrowsAny<ArgumentException>(() => order.MarkFailed(" ", Now));
        Assert.Equal(OrderStatus.Pending, order.Status);
    }
}
