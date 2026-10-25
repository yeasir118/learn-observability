using Grpc.Core;
using Grpc.Core.Interceptors;

namespace TinyShop.PaymentService.Api.Services;

/// <summary>
/// The gRPC equivalent of an exception-handling middleware. Turns anything a handler throws into
/// the status codes contracts.md §9.3 defines, without leaking internals to the caller.
/// </summary>
internal sealed class ExceptionInterceptor(ILogger<ExceptionInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        try
        {
            return await continuation(request, context);
        }
        catch (RpcException)
        {
            // Already a deliberate gRPC status (for example INVALID_ARGUMENT); pass it through.
            throw;
        }
        catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
        {
            // The call's token fires for two different reasons, and callers must be able to tell
            // them apart: order-service keeps an order PENDING on DEADLINE_EXCEEDED (the payment
            // may still have happened), but marks it FAILED on any other error (contracts.md §6).
            // Reporting a passed deadline as CANCELLED would turn "unknown" into "failed".
            if (context.Deadline <= DateTime.UtcNow)
            {
                logger.LogInformation("{Method} ran past the caller's deadline", context.Method);
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "The deadline passed before processing finished."));
            }

            logger.LogInformation("{Method} cancelled by the caller", context.Method);
            throw new RpcException(new Status(StatusCode.Cancelled, "The call was cancelled."));
        }
        catch (Exception exception)
        {
            // Full details go to the log; the caller gets a generic INTERNAL status. Without this,
            // gRPC would answer UNKNOWN ("Exception was thrown by handler").
            logger.LogError(exception, "{Method} failed", context.Method);
            throw new RpcException(new Status(StatusCode.Internal, "Payment processing failed."));
        }
    }
}
