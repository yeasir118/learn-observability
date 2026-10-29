using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TinyShop.OrderService.Application.Common.Exceptions;
using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.OrderService.Api.Errors;

/// <summary>
/// Maps the Application exceptions only order-service has, which can happen anywhere in a request,
/// to their responses (contracts.md §2.2, §6):
/// <code>
/// DependencyUnavailableException -> 502 dependency_failed    (user- or product-service failed)
/// DependencyTimeoutException     -> 504 dependency_timeout   (user- or product-service too slow)
/// </code>
/// "order-db unreachable" (DataStoreUnavailableException -> 503) is handled by the shared handler
/// that AddServiceDefaults registers. Any other exception becomes a 500 internal_error.
/// No response contains exception details or the dependency's name.
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
        (int Status, string Code, string Detail, string Dependency)? mapping = exception switch
        {
            DependencyUnavailableException unavailable => (StatusCodes.Status502BadGateway, OrderErrorCodes.DependencyFailed,
                "A service this request depends on failed. Try again later.", unavailable.Dependency),
            DependencyTimeoutException timeout => (StatusCodes.Status504GatewayTimeout, OrderErrorCodes.DependencyTimeout,
                "A service this request depends on did not answer in time. Try again later.", timeout.Dependency),
            _ => null,
        };

        if (mapping is not { } map)
        {
            return false;
        }

        // The dependency's name goes to the log, never to the client.
        logger.LogWarning(exception, "{Code} while handling {Method} {Path} (dependency: {Dependency})",
            map.Code, httpContext.Request.Method, httpContext.Request.Path, map.Dependency);

        httpContext.Response.StatusCode = map.Status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = map.Status,
                Type = ProblemType.For(map.Code),
                Detail = map.Detail,
            },
        });
    }
}
