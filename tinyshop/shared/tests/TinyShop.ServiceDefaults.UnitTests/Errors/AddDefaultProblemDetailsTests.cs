using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.ServiceDefaults.UnitTests.Errors;

public class AddDefaultProblemDetailsTests
{
    [Fact]
    public void RegistersTheCustomizer_WithTheServicesCodes()
    {
        var services = new ServiceCollection();
        services.AddDefaultProblemDetails(catalog => catalog.Add("example_not_found", "Example not found"));
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<ProblemDetailsOptions>>().Value;
        var problem = new ProblemDetails { Status = 404, Type = ProblemType.For("example_not_found") };
        options.CustomizeProblemDetails!(new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            ProblemDetails = problem,
        });

        Assert.Equal("example_not_found", problem.Extensions["code"]);
        Assert.Equal("Example not found", problem.Title);
    }

    [Fact]
    public void AConflictingServiceCode_FailsAtRegistration()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(
            () => services.AddDefaultProblemDetails(catalog => catalog.Add(ErrorCodes.InternalError, "Oops")));
    }
}
