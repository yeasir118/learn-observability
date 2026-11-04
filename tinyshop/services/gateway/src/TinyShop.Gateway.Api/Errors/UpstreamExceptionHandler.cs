using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TinyShop.ServiceDefaults.Errors;

namespace TinyShop.Gateway.Api.Errors;

/// <summary>
/// The gateway's own errors (contracts.md §2.3): it got no response from the upstream at all.
/// Connection refused or DNS failure → 502 upstream_unreachable; our HttpClient.Timeout expired →
/// 504 upstream_timeout (TS-014, decision 6). Anything else isn't handled here: the shared default
/// makes it a 500 internal_error.
/// </summary>
/// <remarks>
/// Two cases never reach this handler, by design of the exception handler middleware:
/// <list type="bullet">
/// <item>The client disconnected (RequestAborted): there's no one to answer. The middleware logs
/// it at Debug and sends nothing, so a disconnect can't become a 504.</item>
/// <item>The response had already started (the upstream failed mid-body): the status is already
/// sent, so no error response is possible. The middleware rethrows and Kestrel aborts the
/// connection: the client sees a broken response, never a truncated "successful" one.</item>
/// </list>
/// </remarks>
internal sealed class UpstreamExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<UpstreamExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (StatusFor(exception) is not { } status)
        {
            return false;
        }

        var code = status == StatusCodes.Status504GatewayTimeout
            ? GatewayErrorCodes.UpstreamTimeout
            : GatewayErrorCodes.UpstreamUnreachable;

        // Warning, not Error: the gateway works; something behind it doesn't.
        logger.LogWarning(exception, "Upstream failed ({Code}) while forwarding {Method} {Path}",
            code, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Type = ProblemType.For(code),
                // No exception message: it names internal hosts and ports (contracts.md §2.1).
                Detail = status == StatusCodes.Status504GatewayTimeout
                    ? "The service didn't answer in time. Try again later."
                    : "The service is unreachable. Try again later.",
            },
        });
    }

    /// <summary>
    /// The gateway's status for an exception from sending the upstream request, or null when it
    /// isn't an upstream failure.
    /// </summary>
    internal static int? StatusFor(Exception exception) => exception switch
    {
        // HttpClient.Timeout: a TaskCanceledException whose inner exception is a TimeoutException.
        // A cancelled token (the client left) is a TaskCanceledException without it.
        TaskCanceledException { InnerException: TimeoutException } => StatusCodes.Status504GatewayTimeout,

        // No usable response: refused, DNS failure, connection reset, an invalid response.
        HttpRequestException => StatusCodes.Status502BadGateway,

        _ => null,
    };
}
