using TinyShop.Gateway.Api.Forwarding;

namespace TinyShop.Gateway.UnitTests.Forwarding;

/// <summary>The route table is exactly contracts.md §4: no route more, no route less.</summary>
public class GatewayRoutesTests
{
    [Fact]
    public void All_IsExactlyThePublicApi()
    {
        GatewayRoute[] expected =
        [
            new("GET", "/api/products", "product-service"),
            new("GET", "/api/users/{id}", "user-service"),
            new("POST", "/api/orders", "order-service"),
            new("GET", "/api/orders/{id}", "order-service"),
            new("GET", "/api/orders", "order-service"),
        ];

        Assert.Equal(expected.OrderBy(r => r.ToString()), GatewayRoutes.All.OrderBy(r => r.ToString()));
    }
}
