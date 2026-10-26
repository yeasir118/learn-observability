using Grpc.Core;
using V1 = TinyShop.Payment.V1;

namespace TinyShop.OrderService.UnitTests.Infrastructure;

/// <summary>
/// The generated PaymentServiceClient sends every call through a CallInvoker; normally one that
/// speaks HTTP/2 to a channel. This one hands the request to <paramref name="handle"/> instead,
/// which returns a response or throws the RpcException a real call would end with.
/// </summary>
internal sealed class FakePaymentCallInvoker(
    Func<V1.ProcessPaymentRequest, CallOptions, Task<V1.ProcessPaymentResponse>> handle) : CallInvoker
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request)
    {
        var response = InvokeAsync<TResponse>((V1.ProcessPaymentRequest)(object)request, options);

        return new AsyncUnaryCall<TResponse>(
            response,
            Task.FromResult(new Metadata()),
            () => response.IsCompletedSuccessfully ? Status.DefaultSuccess : new Status(StatusCode.Unknown, ""),
            () => [],
            () => { });
    }

    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
        throw new NotSupportedException();

    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options, TRequest request) =>
        throw new NotSupportedException();

    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options) =>
        throw new NotSupportedException();

    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        Method<TRequest, TResponse> method, string? host, CallOptions options) =>
        throw new NotSupportedException();

    private async Task<TResponse> InvokeAsync<TResponse>(V1.ProcessPaymentRequest request, CallOptions options) =>
        (TResponse)(object)await handle(request, options);
}
