using TinyShop.ProductService.Domain.Entities;

namespace TinyShop.ProductService.UnitTests.Domain;

public class ProductTests
{
    [Fact]
    public void Constructor_WithValidValues_SetsProperties()
    {
        var product = new Product("Keyboard", 10000);

        Assert.Equal("Keyboard", product.Name);
        Assert.Equal(10000, product.PriceCents);
    }

    [Fact]
    public void Constructor_WithNameAtMaxLength_Succeeds()
    {
        var name = new string('a', Product.NameMaxLength);

        var product = new Product(name, 1);

        Assert.Equal(name, product.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new Product(name, 100));
    }

    [Fact]
    public void Constructor_WithNullName_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Product(null!, 100));
    }

    [Fact]
    public void Constructor_WithNameLongerThanMax_Throws()
    {
        var name = new string('a', Product.NameMaxLength + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Product(name, 100));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositivePrice_Throws(long priceCents)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product("Keyboard", priceCents));
    }
}
