using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using TinyShop.ProductService.Api.Faults;
using TinyShop.ServiceDefaults.Abstractions.Faults;

namespace TinyShop.ProductService.UnitTests.Api;

/// <summary>
/// The error fault's decision, with a random number the test chooses: a rate between 0 and 1 is
/// tested exactly (TS-016, decision 7). The integration tests cover the service end to end.
/// </summary>
public class ProductFaultFilterTests
{
    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.199, true)]
    [InlineData(0.2, false)]   // [0, 0.2) fails: exactly 20 % of all draws
    [InlineData(0.9, false)]
    public void ErrorRate_FailsExactlyWhenTheDrawIsBelowIt(double draw, bool fails)
    {
        var filter = Filter(new ProductFaultOptions { ErrorRate = 0.2 }, draw);

        Assert.Equal(fails, Throws(filter, Context(productId: 2)));
    }

    [Fact]
    public void ErrorRateZero_NeverFails_AndDrawsNoRandomNumber()
    {
        var random = new FixedRandom(0.0);
        var filter = new ProductFaultFilter(Options.Create(new ProductFaultOptions()), random);

        Assert.False(Throws(filter, Context(productId: 2)));
        Assert.Equal(0, random.Draws);
    }

    [Theory]
    [InlineData(1L, true)]
    [InlineData(2L, false)]
    [InlineData(null, false)]   // GET /products: no ID argument at all
    public void WithProductId_OnlyThatProductCanFail(long? requested, bool fails)
    {
        var filter = Filter(new ProductFaultOptions { ErrorRate = 1, ProductId = 1 }, draw: 0.0);

        Assert.Equal(fails, Throws(filter, Context(requested)));
    }

    [Fact]
    public void InjectedFault_NamesTheSettingAndTheRequest()
    {
        var filter = Filter(new ProductFaultOptions { ErrorRate = 1 }, draw: 0.0);

        var exception = Assert.Throws<InjectedFaultException>(() => filter.OnActionExecuting(Context(productId: 2)));

        Assert.Equal("Injected fault (Faults:ErrorRate=1): GET /products/2 failed on purpose.", exception.Message);
    }

    [Fact]
    public void DescribeActive_ListsTheFaultOnlyWhenItIsOn()
    {
        Assert.Empty(new ProductFaultOptions().DescribeActive());
        Assert.Equal(
            ["Faults:ErrorRate=0.2: 20 % of product requests fail with 500 (GET /products/1 only)"],
            new ProductFaultOptions { ErrorRate = 0.2, ProductId = 1 }.DescribeActive());
    }

    private static ProductFaultFilter Filter(ProductFaultOptions options, double draw) =>
        new(Options.Create(options), new FixedRandom(draw));

    private static bool Throws(ProductFaultFilter filter, ActionExecutingContext context)
    {
        try
        {
            filter.OnActionExecuting(context);
            return false;
        }
        catch (InjectedFaultException)
        {
            return true;
        }
    }

    /// <summary>The context of GET /products/{productId}, or of GET /products when it's null.</summary>
    private static ActionExecutingContext Context(long? productId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "GET";
        httpContext.Request.Path = productId is null ? "/products" : $"/products/{productId}";

        var arguments = new Dictionary<string, object?>();
        if (productId is not null)
        {
            arguments["id"] = productId.Value;
        }

        return new ActionExecutingContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            arguments,
            controller: new object());
    }

    private sealed class FixedRandom(double value) : IFaultRandom
    {
        public int Draws { get; private set; }

        public double NextDouble()
        {
            Draws++;
            return value;
        }
    }
}
