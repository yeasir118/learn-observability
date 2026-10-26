using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Grpc.Core;
using V1 = TinyShop.Payment.V1;

namespace TinyShop.OrderService.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for user-service, product-service and payment-service. The real typed HttpClients
/// and the real generated gRPC client are used; only what sits under them is replaced:
/// <list type="bullet">
///   <item>HTTP: the primary handler (normally a socket) is <see cref="HttpHandler"/>.</item>
///   <item>gRPC: the client's CallInvoker (normally an HTTP/2 channel) is <see cref="PaymentInvoker"/>.</item>
/// </list>
/// Each misbehaviour is what the real network stack would produce: a refused connection throws
/// HttpRequestException, a hung service is ended by HttpClient.Timeout, a failed RPC is an RpcException.
/// </summary>
public sealed class FakeDependencies(Func<Task>? duringPayment = null)
{
    public static readonly Uri UserServiceUrl = new("http://user-service.test/");
    public static readonly Uri ProductServiceUrl = new("http://product-service.test/");
    public static readonly Uri PaymentServiceUrl = new("http://payment-service.test/");

    /// <summary>Every payment request received, by order ID.</summary>
    public ConcurrentDictionary<long, V1.ProcessPaymentRequest> PaymentRequests { get; } = new();

    public HttpMessageHandler HttpHandler => new Handler();

    public CallInvoker PaymentInvoker => new FakePaymentCallInvoker(ProcessPaymentAsync);

    private async Task<V1.ProcessPaymentResponse> ProcessPaymentAsync(V1.ProcessPaymentRequest request, CallOptions options)
    {
        PaymentRequests[request.OrderId] = request;

        switch (request.UserId)
        {
            case Scenarios.Payment.Declined:
                return new V1.ProcessPaymentResponse { Success = false, FailureReason = "insufficient_funds" };
            case Scenarios.Payment.DeclineReason:
                return new V1.ProcessPaymentResponse { Success = false };
            case Scenarios.Payment.Unavailable:
                // What Grpc.Net.Client reports when it can't connect.
                throw new RpcException(new Status(StatusCode.Unavailable, "Error connecting to subchannel.",
                    new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused",
                        new SocketException((int)SocketError.ConnectionRefused))));
            case Scenarios.Payment.Internal:
                throw new RpcException(new Status(StatusCode.Internal, "Payment processing failed."));
            case Scenarios.Payment.InvalidArgument:
                throw new RpcException(new Status(StatusCode.InvalidArgument, "amount_cents must be positive."));
            case Scenarios.Payment.DeadlineExceeded:
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "Deadline Exceeded"));
            case Scenarios.Payment.DatabaseDownDuringPayment when duringPayment is not null:
                await duringPayment();
                break;
        }

        return new V1.ProcessPaymentResponse { Success = true, TransactionId = $"txn_{request.OrderId}" };
    }

    private sealed class Handler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            var id = long.Parse(segments[1]);

            if (uri.Host == UserServiceUrl.Host && segments[0] == "users")
            {
                return await RespondAsync(id, "user_not_found", Scenarios.User.Timeout,
                    () => $$"""{ "id": {{id}}, "name": "User {{id}}" }""", cancellationToken);
            }

            if (uri.Host == ProductServiceUrl.Host && segments[0] == "products")
            {
                if (id == Scenarios.Product.InvalidBody)
                {
                    return Json(HttpStatusCode.OK, """{ "id": "oops" }""");
                }

                var (name, price) = id == Scenarios.Product.Keyboard
                    ? ("Keyboard", Scenarios.Product.KeyboardPriceCents)
                    : ($"Product {id}", Scenarios.Product.DefaultPriceCents);
                return await RespondAsync(id, "product_not_found", Scenarios.Product.Timeout,
                    () => $$"""{ "id": {{id}}, "name": "{{name}}", "priceCents": {{price}} }""", cancellationToken);
            }

            // The real services answer an unmatched route with 404 not_found.
            return Problem(HttpStatusCode.NotFound, "not_found");
        }

        /// <summary>
        /// user-service and product-service share their scenario numbers (404, 400, 500, 502, 504),
        /// so one switch serves both.
        /// </summary>
        private static async Task<HttpResponseMessage> RespondAsync(
            long id, string notFoundCode, long timeoutId, Func<string> found, CancellationToken cancellationToken)
        {
            switch (id)
            {
                case Scenarios.User.NotFound:
                    return Problem(HttpStatusCode.NotFound, notFoundCode);
                case Scenarios.User.BadRequest:
                    return Problem(HttpStatusCode.BadRequest, "validation_failed");
                case Scenarios.User.ServerError:
                    return Problem(HttpStatusCode.InternalServerError, "internal_error");
                case Scenarios.User.Unreachable:
                    throw new HttpRequestException(HttpRequestError.ConnectionError, "Connection refused",
                        new SocketException((int)SocketError.ConnectionRefused));
                default:
                    if (id == timeoutId)
                    {
                        // Never answers. HttpClient.Timeout (shortened by the factory) ends the wait.
                        await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
                    }

                    return Json(HttpStatusCode.OK, found());
            }
        }

        private static HttpResponseMessage Problem(HttpStatusCode status, string code) =>
            Json(status,
                $$"""{ "type": "https://tinyshop.example/problems/{{code}}", "status": {{(int)status}}, "code": "{{code}}" }""",
                "application/problem+json");

        private static HttpResponseMessage Json(HttpStatusCode status, string body, string mediaType = "application/json") =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
    }
}
