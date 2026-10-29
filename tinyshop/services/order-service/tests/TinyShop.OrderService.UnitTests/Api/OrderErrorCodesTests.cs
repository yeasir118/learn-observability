using TinyShop.OrderService.Api.Errors;
using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.OrderService.UnitTests.Api;

/// <summary>
/// Only what order-service adds to the shared error catalog. The customizer itself is tested
/// once, in TinyShop.ServiceDefaults.UnitTests.
/// </summary>
public class OrderErrorCodesTests
{
    [Theory]
    [InlineData(OrderErrorCodes.UserNotFound, "User not found")]
    [InlineData(OrderErrorCodes.ProductNotFound, "Product not found")]
    [InlineData(OrderErrorCodes.OrderNotFound, "Order not found")]
    [InlineData(OrderErrorCodes.DependencyFailed, "Dependency failed")]
    [InlineData(OrderErrorCodes.DependencyTimeout, "Dependency timeout")]
    [InlineData(OrderErrorCodes.PaymentUnavailable, "Payment unavailable")]
    [InlineData(OrderErrorCodes.PaymentTimeout, "Payment timeout")]
    public void Register_AddsEachCodeWithItsTitle(string code, string title)
    {
        Assert.Equal(title, Catalog().TitleFor(code));
    }

    [Theory]
    [InlineData(502, "dependency_failed")]
    [InlineData(504, "dependency_timeout")]
    // The shared mappings are unchanged.
    [InlineData(503, "service_unavailable")]
    [InlineData(500, "internal_error")]
    public void FrameworkGeneratedStatus_GetsOrderServicesCode(int status, string expectedCode)
    {
        Assert.Equal(expectedCode, Catalog().CodeForStatus(status));
    }

    private static ErrorCatalog Catalog()
    {
        var catalog = new ErrorCatalog();
        OrderErrorCodes.Register(catalog);
        return catalog;
    }
}
