using NSubstitute;
using TinyShop.OrderService.Application.Abstractions.Persistence;
using TinyShop.OrderService.Application.Services;
using TinyShop.OrderService.Domain.Entities;

namespace TinyShop.OrderService.UnitTests.Application;

public class OrderQueriesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 14, 30, 5, 123, TimeSpan.Zero);

    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly OrderQueries _queries;

    public OrderQueriesTests() => _queries = new OrderQueries(_orders);

    [Fact]
    public async Task GetById_MapsStatusToUpperCase()
    {
        var order = Order.Place(123, 1, "Keyboard", 10_000, 2, Now);
        order.MarkPaid("txn_1", Now);
        _orders.GetByIdAsync(1001, Arg.Any<CancellationToken>()).Returns(order);

        var dto = await _queries.GetByIdAsync(1001, TestContext.Current.CancellationToken);

        Assert.NotNull(dto);
        Assert.Equal("PAID", dto.Status);
        Assert.Equal(20_000, dto.TotalCents);
        Assert.Equal("txn_1", dto.TransactionId);
    }

    [Fact]
    public async Task GetById_UnknownOrder_ReturnsNull()
    {
        Assert.Null(await _queries.GetByIdAsync(999, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListByUser_KeepsTheRepositorysOrder()
    {
        var newer = Order.Place(123, 2, "Mouse", 5_000, 1, Now.AddMinutes(1));
        var older = Order.Place(123, 1, "Keyboard", 10_000, 1, Now);
        _orders.ListByUserAsync(123, Arg.Any<CancellationToken>()).Returns([newer, older]);

        var list = await _queries.ListByUserAsync(123, TestContext.Current.CancellationToken);

        Assert.Equal(["Mouse", "Keyboard"], list.Select(order => order.ProductName));
        Assert.All(list, order => Assert.Equal("PENDING", order.Status));
    }
}
