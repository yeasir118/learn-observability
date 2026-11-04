using Microsoft.AspNetCore.Diagnostics;

namespace TinyShop.Gateway.Api.Forwarding;

/// <summary>
/// Forwards the current request to an internal service and streams its answer back unchanged
/// (contracts.md §2.4). Two separate HTTP connections: client → gateway, gateway → upstream.
/// </summary>
internal sealed class RequestForwarder(IHttpClientFactory httpClientFactory)
{
    /// <summary>
    /// Sends the request to <paramref name="internalPath"/> on the named <paramref name="upstream"/>
    /// client and writes the upstream's status, headers and body into <c>context.Response</c>.
    /// </summary>
    public async Task ForwardAsync(HttpContext context, string upstream, string internalPath)
    {
        var client = httpClientFactory.CreateClient(upstream);

        // QueryString is "" or "?a=1", so it can be appended as is. Request headers and bodies
        // are forwarded in phase 3, with the hop-by-hop rules.
        using var request = new HttpRequestMessage(
            new HttpMethod(context.Request.Method), internalPath + context.Request.QueryString);

        // ResponseHeadersRead: return once the headers are in; the body is still on the wire.
        // Disposed at the end of the method, after the body has been copied.
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

        // Status and headers first: they go out with the first byte of the body.
        context.Response.StatusCode = (int)response.StatusCode;
        CopyHeaders(response.Headers, context.Response.Headers);
        CopyHeaders(response.Content.Headers, context.Response.Headers);

        // The upstream's own response, errors included, goes through as is: no Problem Details
        // body may be written over an empty 4xx/5xx from the upstream (decision 5).
        // "?.=" (C# 14): assigns only when the feature is present.
        context.Features.Get<IStatusCodePagesFeature>()?.Enabled = false;

        // Streaming: bytes are written to the client as they arrive from the upstream.
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    private static void CopyHeaders(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> from, IHeaderDictionary to)
    {
        foreach (var header in from)
        {
            // Framing belongs to each connection: Kestrel decides its own for the client. Copying
            // the upstream's "chunked" would announce a framing the body isn't written in.
            // The full hop-by-hop list comes in phase 3.
            if (string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            to[header.Key] = header.Value.ToArray();
        }
    }
}
