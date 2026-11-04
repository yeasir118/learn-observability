using System.Net.Sockets;
using TinyShop.Gateway.Api.Errors;

namespace TinyShop.Gateway.UnitTests.Api;

/// <summary>
/// Which exceptions are the gateway's 502 and 504 (TS-014, decision 6). Built the way HttpClient
/// throws them; the integration tests produce the real ones.
/// </summary>
public class UpstreamExceptionHandlerTests
{
    [Fact]
    public void ConnectionRefused_Is502()
    {
        var exception = new HttpRequestException(
            HttpRequestError.ConnectionError, "Connection refused", new SocketException((int)SocketError.ConnectionRefused));

        Assert.Equal(502, UpstreamExceptionHandler.StatusFor(exception));
    }

    [Fact]
    public void HttpClientTimeout_Is504()
    {
        var exception = new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 1 seconds elapsing.",
            new TimeoutException());

        Assert.Equal(504, UpstreamExceptionHandler.StatusFor(exception));
    }

    [Fact]
    public void CancellationWithoutTimeout_IsNotTheGateways()
    {
        // What a client disconnect looks like: cancelled, but not by our timeout.
        Assert.Null(UpstreamExceptionHandler.StatusFor(new TaskCanceledException()));
    }

    [Fact]
    public void AnyOtherException_IsNotTheGateways()
    {
        // Left to the shared handler: 500 internal_error.
        Assert.Null(UpstreamExceptionHandler.StatusFor(new InvalidOperationException()));
    }
}
