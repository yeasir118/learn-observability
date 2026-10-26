namespace TinyShop.OrderService.UnitTests.Infrastructure;

/// <summary>
/// The bottom of an HttpClient's handler pipeline, replaced: instead of opening a connection it
/// runs <paramref name="respond"/>, which can answer, throw (as a refused connection does) or
/// hang (as a dependency that never answers does).
/// </summary>
internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
{
    public List<Uri> RequestedUris { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUris.Add(request.RequestUri!);
        return respond(request, cancellationToken);
    }
}
