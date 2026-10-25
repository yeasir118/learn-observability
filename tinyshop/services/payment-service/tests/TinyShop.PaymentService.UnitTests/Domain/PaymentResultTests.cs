using TinyShop.PaymentService.Domain.Payments;

namespace TinyShop.PaymentService.UnitTests.Domain;

public class PaymentResultTests
{
    [Fact]
    public void Approved_HasTransactionIdAndNoReason()
    {
        var result = PaymentResult.Approved("txn_abc");

        Assert.True(result.Success);
        Assert.Equal("txn_abc", result.TransactionId);
        Assert.Null(result.FailureReason);
    }

    [Fact]
    public void Declined_HasReasonAndNoTransactionId()
    {
        var result = PaymentResult.Declined("insufficient_funds");

        Assert.False(result.Success);
        Assert.Null(result.TransactionId);
        Assert.Equal("insufficient_funds", result.FailureReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Approved_WithoutTransactionId_Throws(string transactionId)
    {
        Assert.Throws<ArgumentException>(() => PaymentResult.Approved(transactionId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Declined_WithoutReason_Throws(string reason)
    {
        Assert.Throws<ArgumentException>(() => PaymentResult.Declined(reason));
    }
}
