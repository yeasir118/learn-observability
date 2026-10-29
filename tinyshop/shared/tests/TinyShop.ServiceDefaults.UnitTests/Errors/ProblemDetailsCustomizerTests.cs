using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.ServiceDefaults.UnitTests.Errors;

public class ProblemDetailsCustomizerTests
{
    // A code a service would add itself; the shared catalog doesn't know it.
    private const string ServiceCode = "example_not_found";

    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(404, "not_found")]
    [InlineData(405, "method_not_allowed")]
    [InlineData(406, "not_acceptable")]
    [InlineData(415, "unsupported_media_type")]
    [InlineData(500, "internal_error")]
    [InlineData(502, "internal_error")]
    [InlineData(503, "service_unavailable")]
    [InlineData(504, "internal_error")]
    public void FrameworkGeneratedProblem_GetsCodeFromStatus(int status, string expectedCode)
    {
        var catalog = new ErrorCatalog();

        var problem = Customize(catalog, new ProblemDetails { Status = status }, "/examples");

        Assert.Equal(expectedCode, problem.Extensions["code"]);
        Assert.Equal($"https://tinyshop.example/problems/{expectedCode}", problem.Type);
        Assert.Equal(catalog.TitleFor(expectedCode), problem.Title);
        Assert.Equal("/examples", problem.Instance);
    }

    [Fact]
    public void FrameworkGeneratedProblem_UsesTheServicesCodeForAStatusItAdded()
    {
        var catalog = new ErrorCatalog().Add("dependency_failed", "Dependency failed", forStatus: 502);

        var problem = Customize(catalog, new ProblemDetails { Status = 502 }, "/examples");

        Assert.Equal("dependency_failed", problem.Extensions["code"]);
        Assert.Equal("Dependency failed", problem.Title);
    }

    [Fact]
    public void ProblemWithOurTypeUri_KeepsThatCode()
    {
        var catalog = new ErrorCatalog().Add(ServiceCode, "Example not found");

        var problem = Customize(
            catalog,
            new ProblemDetails
            {
                Status = 404,
                Type = ProblemType.For(ServiceCode),
                Detail = "Example 999 does not exist.",
            },
            "/examples/999");

        Assert.Equal(ServiceCode, problem.Extensions["code"]);
        Assert.Equal("Example not found", problem.Title);
        Assert.Equal("Example 999 does not exist.", problem.Detail);
    }

    [Fact]
    public void ApiVersioningProblem_IsMappedAndItsDetailDoesNotLeakTheHost()
    {
        var versioningProblem = new ProblemDetails
        {
            Status = 400,
            Type = "https://docs.api-versioning.org/problems#unsupported",
            Detail = "The HTTP resource that matches the request URI 'http://example-service:8080/examples' "
                + "does not support the API version '2.0'.",
        };
        versioningProblem.Extensions["code"] = "UnsupportedApiVersion";

        var problem = Customize(new ErrorCatalog(), versioningProblem, "/examples");

        Assert.Equal("unsupported_api_version", problem.Extensions["code"]);
        Assert.DoesNotContain("example-service", problem.Detail);
    }

    private static ProblemDetails Customize(ErrorCatalog catalog, ProblemDetails problem, string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;

        new ProblemDetailsCustomizer(catalog).Customize(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });

        return problem;
    }
}
