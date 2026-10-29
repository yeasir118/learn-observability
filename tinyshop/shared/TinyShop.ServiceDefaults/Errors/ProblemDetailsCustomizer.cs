using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TinyShop.ServiceDefaults.Errors;

/// <summary>
/// Runs for every Problem Details response, whoever created it: a controller, the exception handler,
/// the status-code-pages middleware, model validation, or the API-versioning library.
/// It makes all of them follow contracts.md §2.1: our <c>type</c> URI, our <c>title</c>,
/// a machine-readable <c>code</c>, and the request path as <c>instance</c>.
/// </summary>
internal sealed class ProblemDetailsCustomizer(ErrorCatalog catalog)
{
    private const string CodeKey = "code";

    // Codes the Asp.Versioning library puts in its own "code" extension.
    private static readonly HashSet<string> ApiVersioningCodes =
    [
        "UnsupportedApiVersion",
        "InvalidApiVersion",
        "AmbiguousApiVersion",
        "ApiVersionUnspecified",
    ];

    public void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;

        var code = ChooseCode(problem);

        problem.Type = ProblemType.For(code);
        problem.Title = catalog.TitleFor(code);
        problem.Instance = context.HttpContext.Request.Path;
        problem.Extensions[CodeKey] = code;

        if (code == ErrorCodes.UnsupportedApiVersion)
        {
            // The library's own detail contains the full request URL, including the host
            // (http://<service>:8080/... inside Docker). Internal hostnames must never reach
            // clients (contracts.md §2.1), so replace it.
            problem.Detail = "The requested API version is not supported. "
                + "The api-supported-versions response header lists the supported versions.";
        }
    }

    private string ChooseCode(ProblemDetails problem)
    {
        // 1. A controller asked for a specific error by setting one of our type URIs.
        if (ProblemType.CodeOf(problem.Type) is { } codeFromType)
        {
            return codeFromType;
        }

        // 2. The API-versioning library rejected the requested version.
        if (problem.Extensions.TryGetValue(CodeKey, out var existing)
            && existing is string versioningCode
            && ApiVersioningCodes.Contains(versioningCode))
        {
            return ErrorCodes.UnsupportedApiVersion;
        }

        // 3. Anything else the framework generated: derive the code from the status.
        return catalog.CodeForStatus(problem.Status);
    }
}
