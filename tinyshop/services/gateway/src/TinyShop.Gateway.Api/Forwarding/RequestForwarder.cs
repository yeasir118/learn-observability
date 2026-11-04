using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Net.Http.Headers;

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

        using var request = BuildUpstreamRequest(context.Request, internalPath);

        // ResponseHeadersRead: return once the headers are in; the body is still on the wire.
        // Disposed at the end of the method, after the body has been copied.
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

        // Status and headers first: they go out with the first byte of the body.
        context.Response.StatusCode = (int)response.StatusCode;

        // One message, one Connection header: it names the hop-by-hop headers in both collections.
        var connection = response.Headers.Connection;
        CopyHeaders(response.Headers, connection, context.Response.Headers);
        CopyHeaders(response.Content.Headers, connection, context.Response.Headers);

        // OriginalString: exactly what the service sent, not Uri's normalized form.
        if (response.Headers.Location is { } location)
        {
            context.Response.Headers.Location = LocationRewriter.ToPublic(location.OriginalString);
        }

        // The upstream's own response, errors included, goes through as is: no Problem Details
        // body may be written over an empty 4xx/5xx from the upstream (decision 5).
        // "?.=" (C# 14): assigns only when the feature is present.
        context.Features.Get<IStatusCodePagesFeature>()?.Enabled = false;

        // Streaming: bytes are written to the client as they arrive from the upstream.
        await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
    }

    /// <summary>The upstream request: method, internal path and query, body, end-to-end headers.</summary>
    private static HttpRequestMessage BuildUpstreamRequest(HttpRequest incoming, string internalPath)
    {
        // QueryString is "" or "?a=1", so it can be appended as is.
        var request = new HttpRequestMessage(new HttpMethod(incoming.Method), internalPath + incoming.QueryString);

        // Only when there is a body. A chunked body has no Content-Length, so checking the length
        // alone would drop it. StreamContent streams it: the body is never held in memory.
        if (incoming.ContentLength > 0 || incoming.Headers.TransferEncoding.Count > 0)
        {
            request.Content = new StreamContent(incoming.Body);
        }

        // StringValues enumerates as string?; OfType<string> drops nulls, giving IEnumerable<string>.
        var connection = incoming.Headers.Connection.OfType<string>().ToArray();
        foreach (var header in incoming.Headers)
        {
            // Host names the gateway; HttpClient sets the upstream's own from the request URI.
            if (string.Equals(header.Key, HeaderNames.Host, StringComparison.OrdinalIgnoreCase)
                || HeaderRules.IsHopByHop(header.Key, connection))
            {
                continue;
            }

            // Headers live in two places in .NET: TryAddWithoutValidation returns false for a content
            // header (Content-Type, Content-Length), which belongs on Content. Without a body there's
            // no Content, and the content header is dropped with it.
            var values = header.Value.ToArray();
            if (!request.Headers.TryAddWithoutValidation(header.Key, values))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, values);
            }
        }

        return request;
    }

    private static void CopyHeaders(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> from, IEnumerable<string> connection, IHeaderDictionary to)
    {
        foreach (var header in from)
        {
            // Framing and keep-alive belong to each connection: Kestrel decides its own for the
            // client. Copying the upstream's would describe a connection the client isn't on.
            if (HeaderRules.IsHopByHop(header.Key, connection))
            {
                continue;
            }

            to[header.Key] = header.Value.ToArray();
        }
    }
}
