using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TinyShop.ServiceDefaults.Abstractions;

namespace TinyShop.ServiceDefaults.Errors;

/// <summary>
/// Maps "the database is unreachable" to 503 service_unavailable (contracts.md §2.2).
/// Any other exception isn't handled here: it goes to the service's own handlers, or to the default
/// handler, which makes it a 500 internal_error. Neither response contains exception details.
/// Registered for every service; in one without a database it simply never sees this exception.
/// </summary>
internal sealed class DataStoreUnavailableExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DataStoreUnavailableExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DataStoreUnavailableException)
        {
            return false;
        }

        logger.LogWarning(exception, "Database unavailable while handling {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Type = ProblemType.For(ErrorCodes.ServiceUnavailable),
                Detail = "The service is temporarily unavailable. Try again later.",
            },
        });
    }
}
