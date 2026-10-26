using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TinyShop.OrderService.Application.Common.Exceptions;

namespace TinyShop.OrderService.Api.Errors;

/// <summary>
/// Maps the Application exceptions that can happen anywhere in a request to their responses
/// (contracts.md §2.2, §6):
/// <code>
/// DataStoreUnavailableException  -> 503 service_unavailable  (order-db unreachable)
/// DependencyUnavailableException -> 502 dependency_failed    (user- or product-service failed)
/// DependencyTimeoutException     -> 504 dependency_timeout   (user- or product-service too slow)
/// </code>
/// Any other exception isn't handled here; it falls through to the default handler and becomes
/// a 500 internal_error. No response contains exception details or the dependency's name.
/// </summary>
internal sealed class ApplicationExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApplicationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int Status, string Code, string Detail)? mapping = exception switch
        {
            DataStoreUnavailableException => (StatusCodes.Status503ServiceUnavailable, ErrorCatalog.ServiceUnavailable,
                "The service is temporarily unavailable. Try again later."),
            DependencyUnavailableException => (StatusCodes.Status502BadGateway, ErrorCatalog.DependencyFailed,
                "A service this request depends on failed. Try again later."),
            DependencyTimeoutException => (StatusCodes.Status504GatewayTimeout, ErrorCatalog.DependencyTimeout,
                "A service this request depends on did not answer in time. Try again later."),
            _ => null,
        };

        if (mapping is not { } map)
        {
            return false;
        }

        // The dependency's name goes to the log, never to the client.
        logger.LogWarning(exception, "{Code} while handling {Method} {Path} (dependency: {Dependency})",
            map.Code, httpContext.Request.Method, httpContext.Request.Path, DependencyOf(exception));

        httpContext.Response.StatusCode = map.Status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = map.Status,
                Type = ErrorCatalog.TypeUri(map.Code),
                Detail = map.Detail,
            },
        });
    }

    private static string DependencyOf(Exception exception) => exception switch
    {
        DependencyUnavailableException unavailable => unavailable.Dependency,
        DependencyTimeoutException timeout => timeout.Dependency,
        _ => "order-db",
    };
}
