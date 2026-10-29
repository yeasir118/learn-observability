using System.Net;
using TinyShop.OrderService.IntegrationTests.Infrastructure;
using static TinyShop.Testing.ResponseAssertions;

namespace TinyShop.OrderService.IntegrationTests.Orders;

/// <summary>GET /orders/{id} and GET /orders?userId= (contracts.md §5.3).</summary>
public class GetOrdersTests(PostgresFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetById_UnknownOrder_Returns404OrderNotFound()
    {
        var response = await _client.GetAsync("/orders/999999999", TestContext.Current.CancellationToken);

        var problem = await AssertProblemAsync(response, HttpStatusCode.NotFound, "order_not_found", "/orders/999999999");
        Assert.Equal("Order 999999999 does not exist.", (string?)problem["detail"]);
    }

    [Fact]
    public async Task GetById_NonIntegerId_Returns400ValidationFailed()
    {
        var response = await _client.GetAsync("/orders/abc", TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed", "/orders/abc");
    }

    [Fact]
    public async Task List_ReturnsTheUsersOrdersNewestFirst_AndNobodyElses()
    {
        var userId = Scenarios.NewUserId();
        var otherUserId = Scenarios.NewUserId();
        var created = new List<long>();
        foreach (var productId in new long[] { 1, 2, 3 })
        {
            var response = await _client.PostOrderAsync(userId, productId, 1);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            created.Add((long)(await OrderApi.ReadObjectAsync(response))["id"]!);
        }

        await _client.PostOrderAsync(otherUserId, 1, 1);

        var items = await _client.ListOrdersAsync(userId);

        Assert.Equal(Enumerable.Reverse(created), items.Select(item => (long)item!["id"]!));
        Assert.All(items, item => Assert.Equal(userId, (long)item!["userId"]!));
        // Each item is a full Order resource.
        Assert.Equal("PAID", (string?)items[0]!["status"]);
        Assert.NotNull(items[0]!["productName"]);
    }

    [Fact]
    public async Task List_UnknownUser_ReturnsEmptyItems()
    {
        var response = await _client.GetAsync($"/orders?userId={Scenarios.NewUserId()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertJsonBodyAsync(response, """{ "items": [] }""");
    }

    [Theory]
    [InlineData("/orders")]
    [InlineData("/orders?userId=abc")]
    public async Task List_MissingOrNonIntegerUserId_Returns400WithUserIdInErrors(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed", "/orders");
        Assert.NotNull(problem["errors"]!["userId"]);
    }
}
