using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TinyShop.ProductService.Application.Abstractions.Persistence;
using TinyShop.ProductService.Application.Models;
using TinyShop.ProductService.Application.Services;
using TinyShop.ProductService.Domain.Entities;

namespace TinyShop.ProductService.UnitTests.Application;

public class ProductCatalogServiceTests
{
    private readonly IProductRepository _repository = Substitute.For<IProductRepository>();
    private readonly IProductCatalogService _service;

    public ProductCatalogServiceTests()
    {
        _service = new ProductCatalogService(_repository, NullLogger<ProductCatalogService>.Instance);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllProductsInRepositoryOrder()
    {
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            CreateProduct(1, "Laptop", 100000),
            CreateProduct(2, "Keyboard", 10000),
        ]);

        var result = await _service.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [new ProductDto(1, "Laptop", 100000), new ProductDto(2, "Keyboard", 10000)],
            result);
    }

    [Fact]
    public async Task GetAllAsync_WhenNoProducts_ReturnsEmptyList()
    {
        _repository.ListAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await _service.GetAllAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductExists_ReturnsIt()
    {
        _repository.GetByIdAsync(2, Arg.Any<CancellationToken>())
            .Returns(CreateProduct(2, "Keyboard", 10000));

        var result = await _service.GetByIdAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(new ProductDto(2, "Keyboard", 10000), result);
    }

    [Fact]
    public async Task GetByIdAsync_WhenProductDoesNotExist_ReturnsNull()
    {
        _repository.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Product?)null);

        var result = await _service.GetByIdAsync(999, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsync_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();

        await _service.GetByIdAsync(1, cts.Token);

        await _repository.Received(1).GetByIdAsync(1, cts.Token);
    }

    /// <summary>
    /// Id has a private setter because the database assigns it. Tests set it through reflection,
    /// the same way EF Core does when it loads a row.
    /// </summary>
    private static Product CreateProduct(long id, string name, long priceCents)
    {
        var product = new Product(name, priceCents);
        typeof(Product).GetProperty(nameof(Product.Id))!.SetValue(product, id);
        return product;
    }
}
