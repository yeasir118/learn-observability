using System.Net;
using TinyShop.ProductService.IntegrationTests.Infrastructure;
using static TinyShop.Testing.ResponseAssertions;

namespace TinyShop.ProductService.IntegrationTests.Products;

/// <summary>GET /products and GET /products/{id} (contracts.md §5.1).</summary>
public class GetProductsTests(PostgresFixture fixture)
{
    private readonly HttpClient _client = fixture.Factory.CreateClient();

    [Fact]
    public async Task GetAll_ReturnsTheFiveSeededProductsInIdOrder()
    {
        var response = await _client.GetAsync("/products", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertJsonBodyAsync(response, """
            {
              "items": [
                { "id": 1, "name": "Laptop",     "priceCents": 100000 },
                { "id": 2, "name": "Keyboard",   "priceCents": 10000 },
                { "id": 3, "name": "Mouse",      "priceCents": 5000 },
                { "id": 4, "name": "Monitor",    "priceCents": 30000 },
                { "id": 5, "name": "Headphones", "priceCents": 15000 }
              ]
            }
            """);
    }

    [Fact]
    public async Task GetById_WhenProductExists_ReturnsIt()
    {
        var response = await _client.GetAsync("/products/2", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertJsonBodyAsync(response, """{ "id": 2, "name": "Keyboard", "priceCents": 10000 }""");
    }

    [Theory]
    [InlineData("/products/999")]
    [InlineData("/products/0")]
    [InlineData("/products/-1")]
    public async Task GetById_WhenProductDoesNotExist_Returns404ProductNotFound(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "product_not_found", path);
    }

    [Theory]
    [InlineData("/products/abc")]
    [InlineData("/products/1.5")]
    [InlineData("/products/99999999999999999999")] // doesn't fit in a 64-bit integer
    public async Task GetById_WhenIdIsNotAValidInteger_Returns400ValidationFailed(string path)
    {
        var response = await _client.GetAsync(path, TestContext.Current.CancellationToken);

        var problem = await AssertProblemAsync(response, HttpStatusCode.BadRequest, "validation_failed", path);

        // Validation errors name the offending field (contracts.md §2.1).
        Assert.NotNull(problem["errors"]?["id"]);
    }
}
