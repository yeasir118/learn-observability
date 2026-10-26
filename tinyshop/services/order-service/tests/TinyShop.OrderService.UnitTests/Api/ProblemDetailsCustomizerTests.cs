using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TinyShop.OrderService.Api.Errors;

namespace TinyShop.OrderService.UnitTests.Api;

public class ProblemDetailsCustomizerTests
{
    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(404, "not_found")]
    [InlineData(405, "method_not_allowed")]
    [InlineData(415, "unsupported_media_type")]
    [InlineData(500, "internal_error")]
    [InlineData(502, "dependency_failed")]
    [InlineData(503, "service_unavailable")]
    [InlineData(504, "dependency_timeout")]
    public void FrameworkGeneratedProblem_GetsCodeFromStatus(int status, string expectedCode)
    {
        var problem = Customize(new ProblemDetails { Status = status }, "/orders");

        Assert.Equal(expectedCode, problem.Extensions["code"]);
        Assert.Equal($"https://tinyshop.example/problems/{expectedCode}", problem.Type);
        Assert.Equal(ErrorCatalog.TitleFor(expectedCode), problem.Title);
        Assert.Equal("/orders", problem.Instance);
    }

    [Fact]
    public void ProblemWithOurTypeUri_KeepsThatCode()
    {
        var problem = Customize(
            new ProblemDetails
            {
                Status = 422,
                Type = ErrorCatalog.TypeUri(ErrorCatalog.ProductNotFound),
                Detail = "Product 999 does not exist.",
            },
            "/orders");

        Assert.Equal("product_not_found", problem.Extensions["code"]);
        Assert.Equal("Product not found", problem.Title);
        Assert.Equal("Product 999 does not exist.", problem.Detail);
    }

    [Fact]
    public void ApiVersioningProblem_IsMappedAndItsDetailDoesNotLeakTheHost()
    {
        var versioningProblem = new ProblemDetails
        {
            Status = 400,
            Type = "https://docs.api-versioning.org/problems#unsupported",
            Detail = "The HTTP resource that matches the request URI 'http://order-service:8080/orders' "
                + "does not support the API version '2.0'.",
        };
        versioningProblem.Extensions["code"] = "UnsupportedApiVersion";

        var problem = Customize(versioningProblem, "/orders");

        Assert.Equal("unsupported_api_version", problem.Extensions["code"]);
        Assert.DoesNotContain("order-service", problem.Detail);
    }

    [Fact]
    public void UnknownCode_FallsBackToInternalErrorTitle()
    {
        Assert.Equal("Internal error", ErrorCatalog.TitleFor("no_such_code"));
    }

    private static ProblemDetails Customize(ProblemDetails problem, string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;

        ProblemDetailsCustomizer.Customize(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });

        return problem;
    }
}
