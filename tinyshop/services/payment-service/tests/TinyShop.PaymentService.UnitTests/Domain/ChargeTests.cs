using TinyShop.PaymentService.Domain.Payments;

namespace TinyShop.PaymentService.UnitTests.Domain;

public class ChargeTests
{
    [Fact]
    public void Constructor_WithValidValues_SetsProperties()
    {
        var charge = new Charge(orderId: 1001, userId: 123, amountCents: 20000);

        Assert.Equal(1001, charge.OrderId);
        Assert.Equal(123, charge.UserId);
        Assert.Equal(20000, charge.AmountCents);
    }

    [Theory]
    [InlineData(0, 123, 100)]
    [InlineData(-1, 123, 100)]
    [InlineData(1001, 0, 100)]
    [InlineData(1001, -5, 100)]
    [InlineData(1001, 123, 0)]
    [InlineData(1001, 123, -100)]
    public void Constructor_WithNonPositiveValue_Throws(long orderId, long userId, long amountCents)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Charge(orderId, userId, amountCents));
    }
}
