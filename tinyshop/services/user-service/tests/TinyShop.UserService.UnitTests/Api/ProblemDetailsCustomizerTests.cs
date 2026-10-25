using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TinyShop.UserService.Api.Errors;

namespace TinyShop.UserService.UnitTests.Api;

public class ProblemDetailsCustomizerTests
{
    [Theory]
    [InlineData(400, "validation_failed")]
    [InlineData(404, "not_found")]
    [InlineData(405, "method_not_allowed")]
    [InlineData(415, "unsupported_media_type")]
    [InlineData(500, "internal_error")]
    [InlineData(503, "service_unavailable")]
    public void FrameworkGeneratedProblem_GetsCodeFromStatus(int status, string expectedCode)
    {
        var problem = Customize(new ProblemDetails { Status = status }, "/users");

        Assert.Equal(expectedCode, problem.Extensions["code"]);
        Assert.Equal($"https://tinyshop.example/problems/{expectedCode}", problem.Type);
        Assert.Equal(ErrorCatalog.TitleFor(expectedCode), problem.Title);
        Assert.Equal("/users", problem.Instance);
    }

    [Fact]
    public void ProblemWithOurTypeUri_KeepsThatCode()
    {
        var problem = Customize(
            new ProblemDetails
            {
                Status = 404,
                Type = ErrorCatalog.TypeUri(ErrorCatalog.UserNotFound),
                Detail = "User 999 does not exist.",
            },
            "/users/999");

        Assert.Equal("user_not_found", problem.Extensions["code"]);
        Assert.Equal("User not found", problem.Title);
        Assert.Equal("User 999 does not exist.", problem.Detail);
    }

    [Fact]
    public void ApiVersioningProblem_IsMappedAndItsDetailDoesNotLeakTheHost()
    {
        var versioningProblem = new ProblemDetails
        {
            Status = 400,
            Type = "https://docs.api-versioning.org/problems#unsupported",
            Detail = "The HTTP resource that matches the request URI 'http://user-service:8080/users' "
                + "does not support the API version '2.0'.",
        };
        versioningProblem.Extensions["code"] = "UnsupportedApiVersion";

        var problem = Customize(versioningProblem, "/users");

        Assert.Equal("unsupported_api_version", problem.Extensions["code"]);
        Assert.DoesNotContain("user-service", problem.Detail);
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
